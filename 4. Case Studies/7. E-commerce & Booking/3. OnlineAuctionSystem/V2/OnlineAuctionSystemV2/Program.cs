using System.Collections.Concurrent;
using System.Collections.Immutable;

// Online Auction System V2 — Fully Thread-Safe
//
// V1 Thread-Safety Gaps:
//   1. _observers (List) — AddObserver during notification crashes
//   2. Double-close race — Scheduler + manual CloseAuction both fire notifications
//   3. Observer list shared across Timer thread and main thread without protection
//
// V2 Fixes:
//   1. Observers → ImmutableList with ImmutableInterlocked (snapshot-safe iteration)
//   2. AuctionItem.Close() returns (winner, alreadyClosed) — idempotent, only first close notifies
//   3. All observer iterations use snapshot from ImmutableList (safe during concurrent add)
//
// Thread-Safety Summary:
//   | Component        | V1                     | V2                                    |
//   |------------------|------------------------|---------------------------------------|
//   | AuctionItem bids | Per-auction lock ✓     | Same (already correct)                |
//   | Observers        | List (unsafe)          | ImmutableList + ImmutableInterlocked  |
//   | Double-close     | Both fire events       | Only first close fires events         |
//   | Auction registry | ConcurrentDictionary ✓ | Same                                 |

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────

// Two-state lifecycle: once Closed, an auction can never reopen.
// The one-way transition simplifies concurrency — Close() is the only state change,
// and it's idempotent (safe to call multiple times).
public enum AuctionStatus { Active, Closed }

// ─────────────────────────────────────────────
// Models
// ─────────────────────────────────────────────

// User is immutable — identity doesn't change after creation.
// Can act as both seller (creates auctions) and buyer (places bids).
public class User
{
    public string Id { get; }
    public string Name { get; }
    public User(string id, string name) { Id = id; Name = name; }
    public override string ToString() => Name;
}

// Bid is immutable after creation — once placed, it's a permanent historical record.
// Timestamp captured at construction for tie-breaking (earliest bid wins among equal amounts).
public class Bid
{
    public string Id { get; }
    public string BidderId { get; }
    public double Amount { get; }
    public DateTime Timestamp { get; }  // UTC for consistent ordering across timezones

    public Bid(string bidderId, double amount)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        BidderId = bidderId;
        Amount = amount;
        Timestamp = DateTime.UtcNow;
    }

    public override string ToString() => $"Bid(₹{Amount} by {BidderId} at {Timestamp:HH:mm:ss.fff})";
}

// ─────────────────────────────────────────────
// AuctionItem — per-item lock, idempotent Close()
// ─────────────────────────────────────────────

// AuctionItem is the core domain object. Key V2 design decisions:
//   1. Per-auction lock: bids on DIFFERENT auctions run in parallel (no global lock).
//      Only bids on the SAME auction serialize against each other.
//   2. PlaceBid does validate + add ATOMICALLY under one lock acquisition.
//      This prevents TOCTOU: two threads can't both see the same "current highest"
//      and both think their bid is valid.
//   3. Close() is IDEMPOTENT: returns (winner, alreadyClosed).
//      If scheduler and manual close race, only the first one fires notifications.
//      The second one gets alreadyClosed=true and skips notification — no duplicates.
public class AuctionItem
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public double StartingPrice { get; }
    public DateTime EndTime { get; }
    public string SellerId { get; }

    // Per-auction lock: scoped to this item only.
    // Two AuctionItems have independent locks — parallel bidding across auctions.
    private readonly object _lock = new();

    // Plain List because all access is guarded by _lock.
    // No need for ConcurrentCollection — we always hold the lock when touching _bids.
    private readonly List<Bid> _bids = new();
    private AuctionStatus _status;

    // Status read under lock ensures cross-thread visibility of the latest value.
    public AuctionStatus Status { get { lock (_lock) { return _status; } } }

    public AuctionItem(string sellerId, string title, string description, double startingPrice, DateTime endTime)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        SellerId = sellerId;
        Title = title;
        Description = description;
        StartingPrice = startingPrice;
        EndTime = endTime;
        _status = AuctionStatus.Active;
    }

    // Returns current highest bid amount (or starting price if no bids).
    // Lock ensures we see a consistent snapshot of _bids.
    public double GetCurrentPrice()
    {
        lock (_lock)
        {
            return _bids.Count == 0 ? StartingPrice : _bids.Max(b => b.Amount);
        }
    }

    // Returns the winning bid: highest amount, tie-broken by earliest timestamp.
    // OrderByDescending(amount) then ThenBy(timestamp) puts the winner first.
    public Bid? GetHighestBid()
    {
        lock (_lock)
        {
            if (_bids.Count == 0) return null;
            return _bids.OrderByDescending(b => b.Amount).ThenBy(b => b.Timestamp).First();
        }
    }

    // Returns a snapshot copy of bid history — safe for callers to iterate
    // without holding the lock (the returned list is a separate object).
    public List<Bid> GetBidHistory()
    {
        lock (_lock) { return _bids.ToList(); }
    }

    // PlaceBid: atomic validate + add under one lock acquisition.
    // Returns:
    //   success: was the bid accepted?
    //   reason: why it was rejected (human-readable)
    //   previousHighest: the bid that was just outbid (for notification)
    //
    // Validation rules (all checked under lock, no TOCTOU gap):
    //   1. Auction must be Active (not Closed)
    //   2. Bidder cannot be the seller (prevents price manipulation)
    //   3. Bid amount must strictly exceed current highest
    public (bool success, string reason, Bid? previousHighest) PlaceBid(Bid bid)
    {
        lock (_lock)
        {
            // Rule 1: Auction must be active
            if (_status == AuctionStatus.Closed)
                return (false, "Auction has ended", null);

            // Rule 2: Seller cannot bid on own item
            if (bid.BidderId == SellerId)
                return (false, "Seller cannot bid on own item", null);

            // Rule 3: Must exceed current price (starting price if no bids)
            double currentPrice = _bids.Count == 0 ? StartingPrice : _bids.Max(b => b.Amount);

            if (bid.Amount <= currentPrice)
                return (false, $"Bid must exceed current price ₹{currentPrice}", null);

            // Capture previous highest BEFORE adding new bid (for outbid notification)
            var previousHighest = GetHighestBidInternal();

            // All validations passed — add the bid
            _bids.Add(bid);
            return (true, "Bid accepted", previousHighest);
        }
    }

    // V2 IDEMPOTENT Close:
    //   - If already Closed: returns (winner, alreadyClosed=true) — caller MUST NOT notify
    //   - If Active (first close): transitions to Closed, returns (winner, alreadyClosed=false) — caller SHOULD notify
    //
    // This eliminates the V1 double-close bug where both scheduler and manual close
    // would fire notifications, resulting in duplicate "auction ended" events.
    public (Bid? winner, bool alreadyClosed) Close()
    {
        lock (_lock)
        {
            if (_status == AuctionStatus.Closed)
                return (GetHighestBidInternal(), true); // Already closed — someone beat us

            _status = AuctionStatus.Closed;
            return (GetHighestBidInternal(), false); // We're the first — notify observers
        }
    }

    // Internal helper — MUST be called under lock (no lock acquisition here)
    private Bid? GetHighestBidInternal()
    {
        if (_bids.Count == 0) return null;
        return _bids.OrderByDescending(b => b.Amount).ThenBy(b => b.Timestamp).First();
    }

    public override string ToString() => $"Auction({Id}: \"{Title}\", ₹{GetCurrentPrice()}, {Status})";
}

// ─────────────────────────────────────────────
// Observer — interface unchanged from V1
// ─────────────────────────────────────────────

// Observer pattern decouples notification logic from auction logic.
// Adding email/push/SMS notifications = just implement IAuctionObserver.
// No changes needed to AuctionItem or AuctionService.
public interface IAuctionObserver
{
    void OnOutbid(AuctionItem item, string outbidUserId, Bid newHighest);
    void OnAuctionEnded(AuctionItem item, Bid? winningBid);
}

// Console implementation — logs events to stdout for demo purposes.
public class ConsoleAuctionObserver : IAuctionObserver
{
    public void OnOutbid(AuctionItem item, string outbidUserId, Bid newHighest)
    {
        Console.WriteLine($"    [Notify] {outbidUserId} outbid on \"{item.Title}\" — new highest: ₹{newHighest.Amount} by {newHighest.BidderId}");
    }

    public void OnAuctionEnded(AuctionItem item, Bid? winningBid)
    {
        if (winningBid != null)
            Console.WriteLine($"    [Notify] Auction \"{item.Title}\" ended! Winner: {winningBid.BidderId} with ₹{winningBid.Amount}");
        else
            Console.WriteLine($"    [Notify] Auction \"{item.Title}\" ended with no bids.");
    }
}

// ─────────────────────────────────────────────
// AuctionScheduler — closes auctions, calls back into service
// ─────────────────────────────────────────────

// V2 Scheduler design: holds a plain reference to AuctionService.
// When an auction expires, it calls service.OnAuctionExpired() — a regular method call.
//
// Why not delegates/Func?
//   - Simpler to understand and explain in interviews
//   - Clear dependency: scheduler → service (visible in constructor)
//   - Service owns observer list, so service handles notifications
//
// Why not per-auction timers?
//   - One timer for all auctions is simpler to manage (no timer leak on many auctions)
//   - 1-second polling interval is acceptable for auction endings
//
// The scheduler uses Close() which is idempotent — if CloseAuction was called manually
// before the scheduler fires, Close() returns alreadyClosed=true and scheduler skips notification.
public class AuctionScheduler : IDisposable
{
    private readonly Timer _timer;
    private readonly ConcurrentDictionary<string, AuctionItem> _auctions;
    private readonly AuctionService _service;  // Plain reference — no delegates

    public AuctionScheduler(ConcurrentDictionary<string, AuctionItem> auctions, AuctionService service)
    {
        _auctions = auctions;
        _service = service;
        // Timer fires every 1 second on a ThreadPool thread.
        // First arg: callback, second: state (null), third: initial delay, fourth: interval.
        _timer = new Timer(CheckAndCloseAuctions, null, 1000, 1000);
    }

    // Runs on ThreadPool thread every 1 second.
    // Scans all auctions, closes expired ones, notifies via service.
    private void CheckAndCloseAuctions(object? state)
    {
        var now = DateTime.UtcNow;
        foreach (var (id, item) in _auctions)
        {
            if (item.Status == AuctionStatus.Active && now >= item.EndTime)
            {
                // Close() is idempotent — only first caller gets alreadyClosed=false
                var (winner, alreadyClosed) = item.Close();
                if (!alreadyClosed)
                {
                    // We're the first to close — notify observers via service
                    _service.OnAuctionExpired(item, winner);
                }
                // If alreadyClosed=true, someone else (manual close) already notified — skip
            }
        }
    }

    // Dispose stops the timer to prevent callbacks after service is gone
    public void Dispose() => _timer.Dispose();
}

// ─────────────────────────────────────────────
// AuctionService — Facade (ImmutableList observers, idempotent close)
// ─────────────────────────────────────────────

// AuctionService is the Facade: simple API hiding locking, scheduling, and observer logic.
//
// V2 key changes from V1:
//   1. _observers is ImmutableList — AddObserver during notification is safe (no crash)
//   2. OnAuctionExpired is a public method called by the scheduler (no delegates)
//   3. CloseAuction checks alreadyClosed before notifying (prevents duplicate events)
//
// Thread-safety:
//   - ConcurrentDictionary for _users and _auctions (lock-free reads/writes)
//   - ImmutableList for _observers (snapshot-safe iteration via ImmutableInterlocked)
//   - Per-auction lock inside AuctionItem for bid serialization
//   - No global lock anywhere — maximum parallelism
public class AuctionService : IDisposable
{
    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly ConcurrentDictionary<string, AuctionItem> _auctions = new();

    // V2: ImmutableList — reading _observers returns a frozen snapshot.
    // Adding an observer creates a NEW ImmutableList without mutating the old one.
    // Any thread iterating the old snapshot continues safely.
    private ImmutableList<IAuctionObserver> _observers = ImmutableList<IAuctionObserver>.Empty;

    private readonly AuctionScheduler _scheduler;

    public AuctionService()
    {
        // Scheduler holds a reference to 'this' and calls OnAuctionExpired when closing auctions.
        // This is safe because the scheduler only calls it from the Timer thread,
        // and OnAuctionExpired reads _observers via snapshot (ImmutableList).
        _scheduler = new AuctionScheduler(_auctions, this);
    }

    // V2: ImmutableInterlocked.Update does an atomic compare-and-swap.
    // Internally: read current → compute new list (with observer added) → CAS.
    // If another thread modified _observers between read and CAS, it retries.
    // Result: no lock needed, no crash during concurrent iteration.
    public void AddObserver(IAuctionObserver observer)
    {
        ImmutableInterlocked.Update(ref _observers, list => list.Add(observer));
    }

    // Called by AuctionScheduler when an auction expires.
    // Also called by CloseAuction for manual close.
    // Service owns the observer list, so all notification logic is centralized here.
    // The `var observers = _observers` captures a snapshot — safe even if AddObserver
    // is called concurrently (new observer will appear in NEXT notification, not this one).
    public void OnAuctionExpired(AuctionItem item, Bid? winner)
    {
        var observers = _observers; // snapshot — immutable, safe to iterate
        foreach (var obs in observers)
            obs.OnAuctionEnded(item, winner);
    }

    // Register a user — TryAdd is idempotent (same ID twice = no-op)
    public User RegisterUser(string id, string name)
    {
        var user = new User(id, name);
        _users.TryAdd(id, user);
        return user;
    }

    // Create an auction listing. Seller must be registered.
    // The auction is immediately live and visible to the scheduler.
    public AuctionItem CreateAuction(string sellerId, string title, string description,
        double startingPrice, DateTime endTime)
    {
        if (!_users.ContainsKey(sellerId))
            throw new ArgumentException($"User '{sellerId}' not found");

        var item = new AuctionItem(sellerId, title, description, startingPrice, endTime);
        _auctions.TryAdd(item.Id, item);
        Console.WriteLine($"    [Auction] Created: \"{title}\" starting at ₹{startingPrice}, ends {endTime:HH:mm:ss}");
        return item;
    }

    // Place a bid: validate inputs → delegate to AuctionItem.PlaceBid → notify observers
    // The per-auction lock inside AuctionItem handles concurrency for same-auction bids.
    public bool PlaceBid(string auctionId, string bidderId, double amount)
    {
        if (!_auctions.TryGetValue(auctionId, out var item))
        {
            Console.WriteLine($"    [Bid] Auction {auctionId} not found");
            return false;
        }

        if (!_users.ContainsKey(bidderId))
        {
            Console.WriteLine($"    [Bid] User {bidderId} not found");
            return false;
        }

        var bid = new Bid(bidderId, amount);
        var (success, reason, previousHighest) = item.PlaceBid(bid);

        if (!success)
        {
            Console.WriteLine($"    [Bid] REJECTED: {reason} (₹{amount} by {bidderId} on \"{item.Title}\")");
            return false;
        }

        Console.WriteLine($"    [Bid] ACCEPTED: ₹{amount} by {bidderId} on \"{item.Title}\"");

        // Notify previous highest bidder they've been outbid.
        // Uses snapshot of _observers — safe even if AddObserver called concurrently.
        if (previousHighest != null && previousHighest.BidderId != bidderId)
        {
            var observers = _observers; // snapshot
            foreach (var obs in observers)
                obs.OnOutbid(item, previousHighest.BidderId, bid);
        }

        return true;
    }

    // Manually close an auction.
    // V2: Only fires notifications if this is the FIRST close (checks alreadyClosed).
    // If scheduler already closed it, alreadyClosed=true and we skip notification.
    public Bid? CloseAuction(string auctionId)
    {
        if (!_auctions.TryGetValue(auctionId, out var item))
            return null;

        var (winner, alreadyClosed) = item.Close();

        // Only the first closer notifies — prevents duplicate events
        if (!alreadyClosed)
            OnAuctionExpired(item, winner);

        return winner;
    }

    // Get winner — only meaningful after auction is closed
    public Bid? GetWinner(string auctionId)
    {
        if (!_auctions.TryGetValue(auctionId, out var item)) return null;
        if (item.Status != AuctionStatus.Closed) return null;
        return item.GetHighestBid();
    }

    // Get bid history — returns a snapshot copy from AuctionItem
    public List<Bid> GetBidHistory(string auctionId)
    {
        if (!_auctions.TryGetValue(auctionId, out var item)) return new List<Bid>();
        return item.GetBidHistory();
    }

    // Dispose stops the scheduler's background timer
    public void Dispose() => _scheduler.Dispose();
}

// ─────────────────────────────────────────────
// Demo — concurrent bids + double-close race
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        using var service = new AuctionService();
        service.AddObserver(new ConsoleAuctionObserver());

        service.RegisterUser("alice", "Alice");
        service.RegisterUser("bob", "Bob");
        service.RegisterUser("charlie", "Charlie");

        // ── Scenario 1: Concurrent bidding war (10 bids from 2 threads) ──
        Console.WriteLine("=== Scenario 1: Concurrent Bidding War ===\n");

        var item = service.CreateAuction("alice", "Gaming Laptop",
            "RTX 4080, 32GB RAM", 50000, DateTime.UtcNow.AddSeconds(10));

        var tasks = new List<Task>();
        for (int i = 1; i <= 10; i++)
        {
            int amount = 50000 + i * 2000; // 52000, 54000, ..., 70000
            string bidder = i % 2 == 0 ? "bob" : "charlie";
            tasks.Add(Task.Run(() => service.PlaceBid(item.Id, bidder, amount)));
        }
        Task.WaitAll(tasks.ToArray());

        Console.WriteLine($"\n    Current highest: ₹{item.GetCurrentPrice()}");
        Console.WriteLine($"    Total bids recorded: {item.GetBidHistory().Count}");

        // ── Scenario 2: Double-close race (scheduler + manual) ──
        Console.WriteLine("\n=== Scenario 2: Double-Close Race ===\n");

        var shortItem = service.CreateAuction("alice", "Quick Auction",
            "Ends in 2 seconds", 1000, DateTime.UtcNow.AddSeconds(2));

        service.PlaceBid(shortItem.Id, "bob", 1500);
        service.PlaceBid(shortItem.Id, "charlie", 2000);

        // Wait until just after endTime, then manually close too
        Console.WriteLine("    Waiting 3 seconds (scheduler will close at 2s)...");
        Thread.Sleep(3000);

        // Manual close — should detect alreadyClosed and NOT fire duplicate notification
        Console.WriteLine("    Manual close attempt (should detect already closed):");
        var winner = service.CloseAuction(shortItem.Id);
        Console.WriteLine($"    Winner: {winner?.BidderId} with ₹{winner?.Amount}");
        Console.WriteLine($"    (Only ONE 'auction ended' notification should have fired above)");

        // ── Scenario 3: Add observer DURING bidding (safe with ImmutableList) ──
        Console.WriteLine("\n=== Scenario 3: Add Observer During Bidding ===\n");

        var item2 = service.CreateAuction("alice", "Rare Book",
            "First edition", 5000, DateTime.UtcNow.AddSeconds(10));

        // Start bidding in background
        var bidTask = Task.Run(() =>
        {
            for (int i = 1; i <= 5; i++)
            {
                service.PlaceBid(item2.Id, "bob", 5000 + i * 500);
                Thread.Sleep(100);
            }
        });

        // Add a second observer mid-flight (V1 would crash, V2 is safe)
        Thread.Sleep(200);
        service.AddObserver(new ConsoleAuctionObserver());
        Console.WriteLine("    (Added second observer mid-flight — no crash!)");

        bidTask.Wait();
        Console.WriteLine("    Bidding complete. No ConcurrentModification crash.");
    }
}
