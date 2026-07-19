using System.Collections.Concurrent;

// Online Auction System V1
//
// Problem Statement:
//   An Online Auction System facilitates buying and selling through competitive bidding
//   over a fixed time window. Sellers list items, buyers place incremental bids.
//
// Core Entities:
//   User                - id, name (can be seller or buyer)
//   AuctionItem         - id, title, description, startingPrice, endTime, status, bids, sellerId
//   Bid                 - id, bidderId, amount, timestamp
//   AuctionStatus       - Active, Closed
//   IAuctionObserver    - notified on outbid and auction end
//   AuctionService      - facade: create auction, place bid, close auction, get winner
//   AuctionScheduler    - background timer that auto-closes auctions at their end time
//
// Design Patterns:
//   - Observer: IAuctionObserver (outbid notifications, auction end)
//   - Facade: AuctionService (simple API hiding bid validation, locking, scheduling)
//   - Repository: in-memory ConcurrentDictionary for users and auctions
//
// Concurrency:
//   - Per-auction lock: each AuctionItem has its own lock for bid placement
//   - Multiple auctions accept bids in parallel (no global lock)
//   - AuctionScheduler runs on a background Timer
//
// Bidding Rules:
//   - Bid must be higher than current highest bid (or starting price if no bids)
//   - Cannot bid on a closed auction
//   - Seller cannot bid on their own item
//   - Winner = highest bid at close; ties resolved by earliest timestamp
//
// Flow:
//   1. Seller creates auction (title, description, startPrice, endTime)
//   2. Buyers place bids (must exceed current highest)
//   3. Observers notified when outbid
//   4. At endTime, AuctionScheduler closes the auction
//   5. Winner determined, observers notified of auction end

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────

// AuctionStatus is kept as a simple two-state enum because an auction
// has a clear, irreversible lifecycle: once closed, it cannot reopen.
// This makes state transitions easy to reason about under concurrency.
public enum AuctionStatus
{
    Active,
    Closed
}

// ─────────────────────────────────────────────
// Models
// ─────────────────────────────────────────────

// User is intentionally simple — it serves as an identity anchor for
// sellers and bidders. All properties are immutable (get-only) because
// a user's identity should never change after creation.
public class User
{
    public string Id { get; }
    public string Name { get; }

    public User(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public override string ToString() => Name;
}

// Bid is a value-like object representing a single bid event.
// It is immutable after creation because bids, once placed, are
// historical records that must not be tampered with.
// The Timestamp is captured at construction time to ensure accurate
// ordering for tie-breaking (earliest bid wins among equal amounts).
public class Bid
{
    public string Id { get; }
    public string BidderId { get; }
    public double Amount { get; }
    public DateTime Timestamp { get; }

    public Bid(string bidderId, double amount)
    {
        // Generate a short unique ID from a GUID for human-readable logging
        Id = Guid.NewGuid().ToString("N")[..8];
        BidderId = bidderId;
        Amount = amount;
        // UTC timestamp ensures consistent ordering regardless of server timezone
        Timestamp = DateTime.UtcNow;
    }

    public override string ToString() => $"Bid(₹{Amount} by {BidderId} at {Timestamp:HH:mm:ss.fff})";
}

// ─────────────────────────────────────────────
// AuctionItem — per-item lock for thread-safe bidding
// ─────────────────────────────────────────────

// AuctionItem encapsulates all auction state and enforces bidding rules.
// The critical design decision here is the PER-AUCTION LOCK: each AuctionItem
// has its own private lock object. This means bids on different auctions can
// proceed in parallel without contention — only bids on the SAME auction
// serialize against each other. A global lock would be a bottleneck in a
// system with thousands of concurrent auctions.
public class AuctionItem
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public double StartingPrice { get; }
    public DateTime EndTime { get; }
    public string SellerId { get; }

    // Per-auction lock object: this is the core concurrency strategy.
    // By scoping the lock to a single auction, we allow maximum parallelism
    // across the system while still guaranteeing atomicity of bid validation
    // and insertion within one auction.
    private readonly object _lock = new();

    // Bids are stored in a plain List because all access is guarded by _lock.
    // A concurrent collection is unnecessary here since we always hold the lock
    // when reading or writing bids.
    private readonly List<Bid> _bids = new();
    private AuctionStatus _status;

    // Status is read under lock to ensure visibility of the latest value
    // across threads (prevents stale reads from CPU caches).
    public AuctionStatus Status
    {
        get { lock (_lock) { return _status; } }
    }

    public AuctionItem(string sellerId, string title, string description, double startingPrice, DateTime endTime)
    {
        // Short GUID prefix for human-readable auction IDs in logs
        Id = Guid.NewGuid().ToString("N")[..8];
        SellerId = sellerId;
        Title = title;
        Description = description;
        StartingPrice = startingPrice;
        EndTime = endTime;
        _status = AuctionStatus.Active;
    }

    // Get current highest bid amount (or starting price if no bids).
    // This is used both for display and for bid validation.
    // The lock ensures we see a consistent snapshot of the bid list.
    public double GetCurrentPrice()
    {
        lock (_lock)
        {
            if (_bids.Count == 0) return StartingPrice;
            return _bids.Max(b => b.Amount);
        }
    }

    // Get the highest bidder (winner if auction is closed).
    // Tie-breaking rule: if two bids have the same amount, the EARLIEST
    // timestamp wins. This is fair because the first person to commit
    // that amount should have priority. OrderByDescending on Amount first,
    // then ThenBy on Timestamp ensures the first element is the winner.
    public Bid? GetHighestBid()
    {
        lock (_lock)
        {
            if (_bids.Count == 0) return null;
            return _bids.OrderByDescending(b => b.Amount).ThenBy(b => b.Timestamp).First();
        }
    }

    // Returns a snapshot (copy) of the bid history so callers can iterate
    // without holding the lock and without risk of concurrent modification.
    public List<Bid> GetBidHistory()
    {
        lock (_lock) { return _bids.ToList(); }
    }

    // Place a bid — the core bidding logic. Returns a tuple with:
    //   - success: whether the bid was accepted
    //   - reason: human-readable explanation for rejection (if any)
    //   - previousHighest: the bid that was just outbid (for observer notification)
    //
    // All validation and mutation happen atomically under the same lock acquisition.
    // This prevents TOCTOU (time-of-check-time-of-use) races where two threads both
    // see the same "current highest" and both think they're placing a valid bid.
    public (bool success, string reason, Bid? previousHighest) PlaceBid(Bid bid)
    {
        lock (_lock)
        {
            // Rule: cannot bid on a closed auction — check status first
            if (_status == AuctionStatus.Closed)
                return (false, "Auction has ended", null);

            // Rule: seller cannot bid on their own item — prevents price manipulation
            if (bid.BidderId == SellerId)
                return (false, "Seller cannot bid on own item", null);

            // Determine the price to beat: starting price if no bids exist yet,
            // otherwise the current maximum bid amount
            double currentPrice = _bids.Count == 0 ? StartingPrice : _bids.Max(b => b.Amount);

            // Rule: bid must strictly exceed the current highest — equal bids are rejected
            // to avoid ambiguity in winner determination
            if (bid.Amount <= currentPrice)
                return (false, $"Bid must exceed current price ₹{currentPrice}", null);

            // Capture the previous highest bidder BEFORE adding the new bid.
            // This is needed so we can notify them that they've been outbid.
            var previousHighest = GetHighestBid();

            // Add the bid — at this point all validations passed
            _bids.Add(bid);
            return (true, "Bid accepted", previousHighest);
        }
    }

    // Close the auction — transitions status to Closed so no further bids are accepted.
    // Returns the winning bid (highest at time of close).
    // Note: In V1, this method is NOT idempotent — calling it twice doesn't cause errors
    // but the caller (scheduler + manual close) may both fire notifications. V2 fixes this.
    public Bid? Close()
    {
        lock (_lock)
        {
            _status = AuctionStatus.Closed;
            return GetHighestBid();
        }
    }

    public override string ToString() => $"Auction({Id}: \"{Title}\", ₹{GetCurrentPrice()}, {Status})";
}

// ─────────────────────────────────────────────
// Observer
// ─────────────────────────────────────────────

// The Observer pattern decouples auction state changes from notification logic.
// This allows adding new notification channels (email, SMS, push) without
// modifying AuctionItem or AuctionService — just implement IAuctionObserver.
public interface IAuctionObserver
{
    // Called when a bidder is outbid by someone else
    void OnOutbid(AuctionItem item, string outbidUserId, Bid newHighest);
    // Called when an auction ends (either manually or by scheduler)
    void OnAuctionEnded(AuctionItem item, Bid? winningBid);
}

// Console implementation of the observer — prints notifications to stdout.
// In a real system, this would be replaced with email/push notification services.
public class ConsoleAuctionObserver : IAuctionObserver
{
    public void OnOutbid(AuctionItem item, string outbidUserId, Bid newHighest)
    {
        Console.WriteLine($"    [Notify] {outbidUserId} was outbid on \"{item.Title}\" — new highest: ₹{newHighest.Amount} by {newHighest.BidderId}");
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
// AuctionScheduler — auto-closes auctions at endTime
// ─────────────────────────────────────────────

// The scheduler runs on a background Timer thread, polling every second to find
// auctions that have passed their EndTime. This design was chosen over per-auction
// timers because:
//   1. Simpler resource management — one timer vs. N timers for N auctions
//   2. Auctions that end within the same second are all closed in one pass
//   3. The 1-second polling interval is acceptable for auction use cases
//      (sub-second precision is rarely needed for auction endings)
//
// KNOWN V1 ISSUE: The scheduler directly iterates the _observers list, which is
// a plain List<T>. If AddObserver is called from another thread during iteration,
// this can throw a ConcurrentModificationException. V2 fixes this with ImmutableList.
public class AuctionScheduler : IDisposable
{
    private readonly Timer _timer;
    private readonly ConcurrentDictionary<string, AuctionItem> _auctions;
    private readonly List<IAuctionObserver> _observers;

    public AuctionScheduler(ConcurrentDictionary<string, AuctionItem> auctions, List<IAuctionObserver> observers)
    {
        _auctions = auctions;
        _observers = observers;
        // Timer fires CheckAndCloseAuctions every 1000ms after an initial 1000ms delay.
        // The Timer runs on a ThreadPool thread, so Close() calls happen off the main thread.
        _timer = new Timer(CheckAndCloseAuctions, null, 1000, 1000);
    }

    // This callback runs on a ThreadPool thread every second.
    // It scans all auctions and closes any that have passed their EndTime.
    private void CheckAndCloseAuctions(object? state)
    {
        var now = DateTime.UtcNow;
        foreach (var (id, item) in _auctions)
        {
            // Only close auctions that are still active and past their end time
            if (item.Status == AuctionStatus.Active && now >= item.EndTime)
            {
                // Close the auction and get the winning bid
                var winningBid = item.Close();

                // Notify all observers that the auction has ended.
                // V1 BUG: If CloseAuction() is called manually at the same time,
                // both the scheduler and the manual call will fire notifications,
                // resulting in duplicate "auction ended" events.
                foreach (var obs in _observers)
                    obs.OnAuctionEnded(item, winningBid);
            }
        }
    }

    public void Dispose() => _timer.Dispose();
}

// ─────────────────────────────────────────────
// AuctionService — Facade
// ─────────────────────────────────────────────

// AuctionService implements the Facade pattern: it provides a simple, unified API
// that hides the complexity of bid validation, per-auction locking, observer
// notification, and background scheduling. Callers don't need to know about
// locks, timers, or observer lists — they just call CreateAuction/PlaceBid/CloseAuction.
//
// ConcurrentDictionary is used for _users and _auctions because these registries
// are accessed from multiple threads (main thread + scheduler's Timer thread).
// This avoids needing a lock around the dictionaries themselves.
public class AuctionService : IDisposable
{
    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly ConcurrentDictionary<string, AuctionItem> _auctions = new();

    // V1 ISSUE: Plain List<T> is not thread-safe for concurrent reads and writes.
    // If AddObserver is called while the scheduler is iterating _observers,
    // it can crash. V2 replaces this with ImmutableList.
    private readonly List<IAuctionObserver> _observers = new();
    private readonly AuctionScheduler _scheduler;

    public AuctionService()
    {
        // Pass the auctions dictionary and observers list to the scheduler
        // so it can close expired auctions and notify observers on its timer thread.
        _scheduler = new AuctionScheduler(_auctions, _observers);
    }

    // Register an observer to receive outbid and auction-end notifications.
    // V1 ISSUE: Not thread-safe — calling this while scheduler iterates _observers is unsafe.
    public void AddObserver(IAuctionObserver observer) => _observers.Add(observer);

    public User RegisterUser(string id, string name)
    {
        var user = new User(id, name);
        // TryAdd is idempotent — registering the same user ID twice is a no-op
        _users.TryAdd(id, user);
        return user;
    }

    // Create an auction listing. Validates that the seller exists before proceeding.
    // The auction is immediately added to the registry and becomes visible to the scheduler.
    public AuctionItem CreateAuction(string sellerId, string title, string description,
        double startingPrice, DateTime endTime)
    {
        // Seller must be registered — prevents ghost auctions from unknown users
        if (!_users.ContainsKey(sellerId))
            throw new ArgumentException($"User '{sellerId}' not found");

        var item = new AuctionItem(sellerId, title, description, startingPrice, endTime);
        // TryAdd to the concurrent dictionary — the auction is now live and
        // the scheduler will pick it up on its next tick if endTime has passed
        _auctions.TryAdd(item.Id, item);
        Console.WriteLine($"    [Auction] Created: \"{title}\" starting at ₹{startingPrice}, ends {endTime:HH:mm:ss}");
        return item;
    }

    // Place a bid on an auction. This method orchestrates:
    //   1. Lookup validation (auction exists, user exists)
    //   2. Delegation to AuctionItem.PlaceBid (which does atomic bid validation under lock)
    //   3. Observer notification for outbid events
    public bool PlaceBid(string auctionId, string bidderId, double amount)
    {
        // Step 1: Verify the auction exists
        if (!_auctions.TryGetValue(auctionId, out var item))
        {
            Console.WriteLine($"    [Bid] Auction {auctionId} not found");
            return false;
        }

        // Step 2: Verify the bidder is a registered user
        if (!_users.ContainsKey(bidderId))
        {
            Console.WriteLine($"    [Bid] User {bidderId} not found");
            return false;
        }

        // Step 3: Create the bid object and delegate to AuctionItem for
        // atomic validation (amount check, seller check, status check)
        var bid = new Bid(bidderId, amount);
        var (success, reason, previousHighest) = item.PlaceBid(bid);

        if (!success)
        {
            Console.WriteLine($"    [Bid] REJECTED: {reason} (₹{amount} by {bidderId} on \"{item.Title}\")");
            return false;
        }

        Console.WriteLine($"    [Bid] ACCEPTED: ₹{amount} by {bidderId} on \"{item.Title}\"");

        // Step 4: Notify the previous highest bidder that they've been outbid.
        // Only notify if there WAS a previous bidder and it's not the same person
        // (no need to notify yourself that you outbid yourself with a higher amount).
        if (previousHighest != null && previousHighest.BidderId != bidderId)
        {
            foreach (var obs in _observers)
                obs.OnOutbid(item, previousHighest.BidderId, bid);
        }

        return true;
    }

    // Manually close an auction (or let the scheduler do it automatically).
    // V1 ISSUE: If the scheduler also closes this auction at the same time,
    // both will fire OnAuctionEnded notifications — resulting in duplicates.
    public Bid? CloseAuction(string auctionId)
    {
        if (!_auctions.TryGetValue(auctionId, out var item))
            return null;

        // Close the auction and determine the winner
        var winner = item.Close();

        // Notify all observers that the auction has ended
        foreach (var obs in _observers)
            obs.OnAuctionEnded(item, winner);
        return winner;
    }

    // Get winner of a closed auction. Returns null if auction doesn't exist
    // or hasn't been closed yet (winner is only meaningful after close).
    public Bid? GetWinner(string auctionId)
    {
        if (!_auctions.TryGetValue(auctionId, out var item)) return null;
        if (item.Status != AuctionStatus.Closed) return null;
        return item.GetHighestBid();
    }

    // Get bid history for an auction — returns a snapshot copy for safe iteration
    public List<Bid> GetBidHistory(string auctionId)
    {
        if (!_auctions.TryGetValue(auctionId, out var item)) return new List<Bid>();
        return item.GetBidHistory();
    }

    // Dispose stops the scheduler's background timer to prevent it from
    // firing after the service is no longer in use
    public void Dispose() => _scheduler.Dispose();
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        // Using statement ensures the scheduler timer is disposed on exit
        using var service = new AuctionService();
        service.AddObserver(new ConsoleAuctionObserver());

        // Register users
        service.RegisterUser("alice", "Alice");
        service.RegisterUser("bob", "Bob");
        service.RegisterUser("charlie", "Charlie");
        service.RegisterUser("dave", "Dave");

        // ── Scenario 1: Basic bidding war ──
        Console.WriteLine("=== Scenario 1: Bidding War on Vintage Watch ===\n");

        var watch = service.CreateAuction("alice", "Vintage Watch",
            "1960s Omega Seamaster", 5000, DateTime.UtcNow.AddSeconds(10));

        service.PlaceBid(watch.Id, "bob", 5500);
        service.PlaceBid(watch.Id, "charlie", 6000);
        service.PlaceBid(watch.Id, "bob", 7000);     // Bob outbids Charlie
        service.PlaceBid(watch.Id, "charlie", 7500);  // Charlie outbids Bob

        // ── Scenario 2: Invalid bids ──
        Console.WriteLine("\n=== Scenario 2: Invalid Bids ===\n");

        // Bid lower than current highest
        service.PlaceBid(watch.Id, "dave", 3000);

        // Seller bidding on own item
        service.PlaceBid(watch.Id, "alice", 10000);

        // ── Scenario 3: Concurrent bids on same auction ──
        Console.WriteLine("\n=== Scenario 3: Concurrent Bids ===\n");

        var painting = service.CreateAuction("alice", "Abstract Painting",
            "Oil on canvas, 2024", 10000, DateTime.UtcNow.AddSeconds(10));

        var tasks = new List<Task>();
        // 5 users bid concurrently with increasing amounts
        for (int i = 0; i < 5; i++)
        {
            int amount = 10000 + (i + 1) * 1000; // 11000, 12000, ..., 15000
            string bidder = i % 2 == 0 ? "bob" : "charlie";
            tasks.Add(Task.Run(() => service.PlaceBid(painting.Id, bidder, amount)));
        }
        Task.WaitAll(tasks.ToArray());

        // ── Scenario 4: Manual close + winner ──
        Console.WriteLine("\n=== Scenario 4: Close Auction + Determine Winner ===\n");

        var winner = service.CloseAuction(watch.Id);
        Console.WriteLine($"\n    Winner of \"{watch.Title}\": {winner?.BidderId} with ₹{winner?.Amount}");

        // ── Scenario 5: Bid after auction closed ──
        Console.WriteLine("\n=== Scenario 5: Bid After Close (should fail) ===\n");
        service.PlaceBid(watch.Id, "dave", 20000);

        // ── Bid history ──
        Console.WriteLine("\n=== Bid History: Vintage Watch ===\n");
        foreach (var bid in service.GetBidHistory(watch.Id))
            Console.WriteLine($"    {bid}");

        // ── Scenario 6: Auto-close by scheduler ──
        Console.WriteLine("\n=== Scenario 6: Auto-Close (wait for scheduler) ===\n");

        var shortAuction = service.CreateAuction("dave", "Quick Item",
            "Expires in 3 seconds", 100, DateTime.UtcNow.AddSeconds(3));

        service.PlaceBid(shortAuction.Id, "bob", 200);
        service.PlaceBid(shortAuction.Id, "charlie", 350);

        Console.WriteLine("    Waiting 4 seconds for auto-close...");
        Thread.Sleep(4000);
        Console.WriteLine($"    Status: {shortAuction.Status}");
        var autoWinner = service.GetWinner(shortAuction.Id);
        Console.WriteLine($"    Winner: {autoWinner?.BidderId} with ₹{autoWinner?.Amount}");
    }
}
