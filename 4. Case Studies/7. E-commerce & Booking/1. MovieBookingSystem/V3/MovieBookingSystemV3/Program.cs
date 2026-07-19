using System.Collections.Concurrent;
using System.Collections.Immutable;

// Movie Ticket Booking System V3
//
// Extends V2 with:
//   1. Automatic Lock Timeout — seats are auto-released if not confirmed within N seconds
//      - SeatLockManager stores lock timestamps
//      - A background Timer periodically scans for expired locks and releases them
//      - If payment takes too long, the lock expires and seats return to Available
//
//   2. Payment Guard Logic — BookingManager checks if lock is still valid AFTER payment
//      - Payment can be slow (network call). Lock might expire during payment.
//      - After Pay() returns, BookingManager verifies the lock is still held by this user.
//      - If lock expired (timeout released it), booking fails even if payment succeeded.
//      - This prevents: lock → timeout releases → another user books → first user confirms (corruption)
//
// Flow:
//   BookTickets(userId, show, seats, paymentStrategy)
//     1. LockSeats(show, seats, userId) → locks + records timestamp
//     2. CalculatePrice()
//     3. Pay() → may be slow (simulated delay)
//     4. GUARD: VerifyLock(show, seats, userId) → are seats STILL locked by this user?
//        - YES → ConfirmSeats() → Booked
//        - NO  → Lock expired during payment → Booking FAILS (refund would happen here)
//
// Background Timer:
//   Every 1 second, SeatLockManager scans all locks.
//   If any lock is older than LOCK_TIMEOUT_MS, it auto-releases those seats.

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────

// Compact enum declarations — same semantics as V1/V2.
public enum SeatType { Regular, Premium, Recliner }
public enum SeatStatus { Available, Locked, Booked }
public enum PaymentStatus { Pending, Success, Failure, Refunded }

// ─────────────────────────────────────────────
// Models (same as V2)
// ─────────────────────────────────────────────

// All model classes carry over V2's thread-safety design: immutable where possible,
// volatile + internal setter for Seat status, ImmutableList for Screen seats.

public class User
{
    public string Id { get; }
    public string Name { get; }
    public string Email { get; }
    public User(string id, string name, string email) { Id = id; Name = name; Email = email; }
}

public class Movie
{
    public string Id { get; }
    public string Title { get; }
    public int DurationMinutes { get; }
    public Movie(string id, string title, int duration) { Id = id; Title = title; DurationMinutes = duration; }
}

public class City
{
    public string Id { get; }
    public string Name { get; }
    public City(string id, string name) { Id = id; Name = name; }
}

// Seat retains V2's volatile status field and internal setter.
// The background timer in SeatLockManager reads status under the per-show lock,
// so volatile ensures the timer thread sees the latest value.
public class Seat
{
    public string Id { get; }
    public int Row { get; }
    public int Col { get; }
    public SeatType Type { get; }
    private volatile SeatStatus _status;
    public SeatStatus Status => _status;
    internal void SetStatus(SeatStatus status) => _status = status;

    public Seat(string id, int row, int col, SeatType type)
    {
        Id = id; Row = row; Col = col; Type = type; _status = SeatStatus.Available;
    }
    public override string ToString() => $"Seat({Id}, R{Row}C{Col}, {Type}, {Status})";
}

// Screen uses ImmutableList (same as V2) for lock-free concurrent reads.
public class Screen
{
    public string Id { get; }
    private ImmutableList<Seat> _seats = ImmutableList<Seat>.Empty;
    public ImmutableList<Seat> Seats => _seats;
    public Screen(string id) => Id = id;
    public void AddSeat(Seat seat) => ImmutableInterlocked.Update(ref _seats, list => list.Add(seat));
}

public class Cinema
{
    public string Id { get; }
    public string Name { get; }
    public City City { get; }
    public List<Screen> Screens { get; }
    public Cinema(string id, string name, City city, List<Screen> screens)
    { Id = id; Name = name; City = city; Screens = screens; }
}

public class Show
{
    public string Id { get; }
    public Movie Movie { get; }
    public Screen Screen { get; }
    public DateTime StartTime { get; }
    public IPricingStrategy PricingStrategy { get; }
    public Show(string id, Movie movie, Screen screen, DateTime startTime, IPricingStrategy pricing)
    { Id = id; Movie = movie; Screen = screen; StartTime = startTime; PricingStrategy = pricing; }
}

public class Payment
{
    public string Id { get; }
    public double Amount { get; }
    public PaymentStatus Status { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public Payment(string id, double amount) { Id = id; Amount = amount; Status = PaymentStatus.Pending; }
}

// Booking is a pure data record — no behavior. Confirmation is done externally by SeatLockManager.
public class Booking
{
    public string Id { get; }
    public User User { get; }
    public Show Show { get; }
    public List<Seat> Seats { get; }
    public double TotalAmount { get; }
    public Payment Payment { get; }
    public Booking(string id, User user, Show show, List<Seat> seats, double totalAmount, Payment payment)
    { Id = id; User = user; Show = show; Seats = seats; TotalAmount = totalAmount; Payment = payment; }
}

// ─────────────────────────────────────────────
// Strategy: Pricing
// ─────────────────────────────────────────────

// Stateless strategy — thread-safe, no mutable fields.
public interface IPricingStrategy
{
    double CalculatePrice(List<Seat> seats);
}

public class WeekdayPricingStrategy : IPricingStrategy
{
    public double CalculatePrice(List<Seat> seats)
    {
        double total = 0;
        foreach (var seat in seats)
        {
            if (seat.Type == SeatType.Premium) total += 350;
            else if (seat.Type == SeatType.Recliner) total += 500;
            else total += 200;
        }
        return total;
    }
}

// ─────────────────────────────────────────────
// Payment: PaymentType + IPaymentMethod + Factory + Processor
// ─────────────────────────────────────────────

public enum PaymentType
{
    CreditCard,
    UPI,
    Wallet
}

public interface IPaymentMethod
{
    Payment Pay(double amount);
    void Refund(Payment payment);
}

// Fast payment — completes instantly (happy path, within lock timeout).
public class CreditCardPayment : IPaymentMethod
{
    public Payment Pay(double amount)
    {
        var payment = new Payment(Guid.NewGuid().ToString("N")[..8], amount);
        payment.Status = PaymentStatus.Success;
        payment.TransactionId = $"TXN-CC-{Guid.NewGuid().ToString("N")[..6]}";
        Console.WriteLine($"    [CreditCard] Charged ₹{amount}. TXN: {payment.TransactionId}");
        return payment;
    }

    public void Refund(Payment payment)
    {
        payment.Status = PaymentStatus.Refunded;
        Console.WriteLine($"    [CreditCard] Refunded ₹{payment.Amount}. TXN: {payment.TransactionId}");
    }
}

public class UPIPayment : IPaymentMethod
{
    public Payment Pay(double amount)
    {
        var payment = new Payment(Guid.NewGuid().ToString("N")[..8], amount);
        payment.Status = PaymentStatus.Success;
        payment.TransactionId = $"TXN-UPI-{Guid.NewGuid().ToString("N")[..6]}";
        Console.WriteLine($"    [UPI] Charged ₹{amount}. TXN: {payment.TransactionId}");
        return payment;
    }

    public void Refund(Payment payment)
    {
        payment.Status = PaymentStatus.Refunded;
        Console.WriteLine($"    [UPI] Refunded ₹{payment.Amount}. TXN: {payment.TransactionId}");
    }
}

public class WalletPayment : IPaymentMethod
{
    public Payment Pay(double amount)
    {
        var payment = new Payment(Guid.NewGuid().ToString("N")[..8], amount);
        payment.Status = PaymentStatus.Success;
        payment.TransactionId = $"TXN-WAL-{Guid.NewGuid().ToString("N")[..6]}";
        Console.WriteLine($"    [Wallet] Deducted ₹{amount}. TXN: {payment.TransactionId}");
        return payment;
    }

    public void Refund(Payment payment)
    {
        payment.Status = PaymentStatus.Refunded;
        Console.WriteLine($"    [Wallet] Refunded ₹{payment.Amount} to wallet. TXN: {payment.TransactionId}");
    }
}

// Slow payment — simulates a network delay that exceeds the lock timeout.
public class SlowPayment : IPaymentMethod
{
    private readonly int _delayMs;
    public SlowPayment(int delayMs) => _delayMs = delayMs;

    public Payment Pay(double amount)
    {
        Console.WriteLine($"    [Payment] Processing... (simulating {_delayMs}ms delay)");
        Thread.Sleep(_delayMs);
        var payment = new Payment(Guid.NewGuid().ToString("N")[..8], amount);
        payment.Status = PaymentStatus.Success;
        payment.TransactionId = $"TXN-SLOW-{Guid.NewGuid().ToString("N")[..6]}";
        Console.WriteLine($"    [Payment] Payment completed after delay. TXN: {payment.TransactionId}");
        return payment;
    }

    public void Refund(Payment payment)
    {
        payment.Status = PaymentStatus.Refunded;
        Console.WriteLine($"    [SlowPayment] Refunded ₹{payment.Amount}. TXN: {payment.TransactionId}");
    }
}

public static class PaymentMethodFactory
{
    public static IPaymentMethod Create(PaymentType type)
    {
        if (type == PaymentType.CreditCard) return new CreditCardPayment();
        else if (type == PaymentType.UPI) return new UPIPayment();
        else if (type == PaymentType.Wallet) return new WalletPayment();
        else throw new ArgumentException($"Unknown payment type: {type}");
    }

    // Overload for testing: allows injecting a custom payment method (e.g., SlowPayment)
    public static IPaymentMethod Create(IPaymentMethod custom) => custom;
}

// PaymentProcessor — resolves method from type and calls Pay().
// Also supports direct IPaymentMethod injection for testing slow/failing payments.
public class PaymentProcessor
{
    public Payment Process(PaymentType type, double amount)
    {
        IPaymentMethod method = PaymentMethodFactory.Create(type);
        return method.Pay(amount);
    }

    public Payment Process(IPaymentMethod method, double amount)
    {
        return method.Pay(amount);
    }

    // Refund a successful payment — called when lock expires after payment
    public void Refund(PaymentType type, Payment payment)
    {
        IPaymentMethod method = PaymentMethodFactory.Create(type);
        method.Refund(payment);
    }

    public void Refund(IPaymentMethod method, Payment payment)
    {
        method.Refund(payment);
    }
}

// ─────────────────────────────────────────────
// Observer (thread-safe, same as V2)
// ─────────────────────────────────────────────

public interface IMovieObserver { void Update(Movie movie); }

public class UserObserver : IMovieObserver
{
    private readonly User _user;
    public UserObserver(User user) => _user = user;
    public void Update(Movie movie) =>
        Console.WriteLine($"    [Notify] {_user.Name}: New movie added - \"{movie.Title}\"");
}

// ImmutableList + ImmutableInterlocked for lock-free observer management (same as V2).
public class MovieSubject
{
    private ImmutableList<IMovieObserver> _observers = ImmutableList<IMovieObserver>.Empty;
    public void AddObserver(IMovieObserver observer) =>
        ImmutableInterlocked.Update(ref _observers, list => list.Add(observer));
    public void NotifyObservers(Movie movie)
    {
        foreach (var observer in _observers) observer.Update(movie);
    }
}

// ─────────────────────────────────────────────
// SeatLockManager — with auto-expiry timer
// ─────────────────────────────────────────────

// V3 SeatLockManager extends V2 with two critical features:
// 1. Lock timestamps — records WHEN each seat was locked so we can detect expiry.
// 2. Background cleanup timer — periodically scans for expired locks and releases them.
//
// Why auto-expiry is necessary:
// Without it, if a user locks seats and then abandons the flow (closes browser, network dies),
// those seats stay locked forever. The timeout ensures seats return to the pool automatically.
//
// IDisposable is implemented to cleanly stop the background timer when the service shuts down.
public class SeatLockManager : IDisposable
{
    // Configurable timeout — short for demos (5s), longer in production (5-15 minutes).
    // Shorter timeout = less waiting for abandoned seats, but more risk of expiring during
    // legitimate slow payments. Tuning this is a business decision.
    private readonly long _lockTimeoutMs;

    // Background timer that runs CleanupExpiredLocks every 1 second.
    // Using Timer (not Task.Delay loop) because it's fire-and-forget with no async overhead.
    // The 1-second interval means worst case a lock lives 1 second past its timeout.
    private readonly Timer _cleanupTimer;

    // Per-show lock objects — same as V2 for parallel show booking.
    private readonly ConcurrentDictionary<string, object> _showLocks = new();

    // show → (seatId → LockInfo). LockInfo stores userId AND timestamp.
    // The timestamp is what enables expiry detection.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, LockInfo>> _lockedSeats = new();

    // Registry of Seat objects by ID — needed so the cleanup timer can call seat.SetStatus()
    // even though it only has the seatId from the lock map.
    private readonly ConcurrentDictionary<string, Seat> _seatRegistry = new();

    public SeatLockManager(long lockTimeoutMs = 5000) // default 5 seconds for demo
    {
        _lockTimeoutMs = lockTimeoutMs;

        // Timer starts after 1 second, then fires every 1 second.
        // The callback (CleanupExpiredLocks) acquires per-show locks, so it's safe
        // against concurrent booking operations.
        _cleanupTimer = new Timer(CleanupExpiredLocks, null, 1000, 1000);
    }

    private object GetShowLock(string showId) => _showLocks.GetOrAdd(showId, _ => new object());

    // Lock seats with timestamp. Returns true if all locked successfully.
    // V3 addition: records the current UTC time so the cleanup timer knows when locks expire.
    public bool LockSeats(Show show, List<Seat> seats, string userId)
    {
        var showLock = GetShowLock(show.Id);

        lock (showLock)
        {
            var seatLocks = _lockedSeats.GetOrAdd(show.Id, _ => new ConcurrentDictionary<string, LockInfo>());

            // Validate all seats available — same all-or-nothing check as V2
            foreach (var seat in seats)
            {
                if (seat.Status != SeatStatus.Available) return false;
                if (seatLocks.ContainsKey(seat.Id)) return false;
            }

            // Lock all + record timestamp for expiry tracking
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var seat in seats)
            {
                seat.SetStatus(SeatStatus.Locked);
                seatLocks.TryAdd(seat.Id, new LockInfo(userId, now));
                // Register seat object so cleanup timer can access it by ID later
                _seatRegistry.TryAdd(seat.Id, seat);
            }
            return true;
        }
    }

    // VerifyLock: the "payment guard" — checks if locks are still held AFTER payment.
    // This is the critical V3 innovation. The scenario it prevents:
    //   1. User A locks seats at T=0
    //   2. Payment takes 10 seconds (slow gateway)
    //   3. At T=5, cleanup timer expires the lock → seats become Available
    //   4. At T=6, User B locks and books those same seats
    //   5. At T=10, User A's payment succeeds
    //   6. WITHOUT this guard, User A would confirm → double-booking!
    //   7. WITH this guard, User A detects the lock is gone → booking fails safely
    public bool VerifyLock(Show show, List<Seat> seats, string userId)
    {
        var showLock = GetShowLock(show.Id);

        lock (showLock)
        {
            if (!_lockedSeats.TryGetValue(show.Id, out var seatLocks)) return false;

            foreach (var seat in seats)
            {
                // Check 1: Is this seat still in our lock tracking map?
                if (!seatLocks.TryGetValue(seat.Id, out var lockInfo)) return false;
                // Check 2: Is it still locked by THIS user? (not someone else who re-locked it)
                if (lockInfo.UserId != userId) return false;
                // Check 3: Is the seat still in Locked state? (not released by timer)
                if (seat.Status != SeatStatus.Locked) return false;
            }
            return true;
        }
    }

    // Confirm: Locked → Booked (after payment + guard passes).
    // Returns true if all seats confirmed successfully, false if lock was already released.
    public bool ConfirmSeats(Show show, List<Seat> seats, string userId)
    {
        var showLock = GetShowLock(show.Id);

        lock (showLock)
        {
            if (!_lockedSeats.TryGetValue(show.Id, out var seatLocks)) return false;

            // Verify all seats are still locked by this user before confirming any
            foreach (var seat in seats)
            {
                if (!seatLocks.TryGetValue(seat.Id, out var lockInfo) || lockInfo.UserId != userId)
                    return false;
                if (seat.Status != SeatStatus.Locked)
                    return false;
            }

            // All valid — confirm atomically
            foreach (var seat in seats)
            {
                seat.SetStatus(SeatStatus.Booked);
                seatLocks.TryRemove(seat.Id, out _);
            }
            return true;
        }
    }

    // Unlock: Locked → Available (manual unlock on payment failure).
    // Different from timer-based cleanup: this is explicitly triggered by the booking flow.
    public void UnlockSeats(Show show, List<Seat> seats, string userId)
    {
        var showLock = GetShowLock(show.Id);

        lock (showLock)
        {
            if (!_lockedSeats.TryGetValue(show.Id, out var seatLocks)) return;

            foreach (var seat in seats)
            {
                if (seatLocks.TryGetValue(seat.Id, out var lockInfo) && lockInfo.UserId == userId)
                {
                    seat.SetStatus(SeatStatus.Available);
                    seatLocks.TryRemove(seat.Id, out _);
                }
            }
        }
    }

    // Background cleanup: auto-release expired locks.
    // This runs on a ThreadPool thread every 1 second via the Timer.
    // It acquires per-show locks to safely modify seat state — no races with booking operations.
    // The cleanup timer is the "safety net" that prevents seat starvation from abandoned flows.
    private void CleanupExpiredLocks(object? state)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var (showId, seatLocks) in _lockedSeats)
        {
            var showLock = GetShowLock(showId);

            lock (showLock)
            {
                // Find all locks that have exceeded the timeout threshold
                var expiredSeats = seatLocks
                    .Where(kv => (now - kv.Value.LockedAtMs) > _lockTimeoutMs)
                    .ToList();

                foreach (var (seatId, lockInfo) in expiredSeats)
                {
                    // Double-check: only release if seat is still Locked (not already Booked/Available)
                    if (_seatRegistry.TryGetValue(seatId, out var seat) && seat.Status == SeatStatus.Locked)
                    {
                        seat.SetStatus(SeatStatus.Available);
                        seatLocks.TryRemove(seatId, out _);
                        // Log expiry — helps operators monitor abandoned booking flows
                        Console.WriteLine($"    [LockManager] EXPIRED: Seat {seatId} released (was locked by {lockInfo.UserId})");
                    }
                }
            }
        }
    }

    // Dispose stops the background timer to prevent it from firing after the service is gone.
    // Without this, the timer callback could reference disposed objects.
    public void Dispose() => _cleanupTimer.Dispose();

    // Internal record for lock metadata — stores WHO locked it and WHEN.
    // Using a record for value equality semantics and concise declaration.
    private record LockInfo(string UserId, long LockedAtMs);
}

// ─────────────────────────────────────────────
// BookingManager — with payment guard
// ─────────────────────────────────────────────

// V3 BookingManager adds step 4 (VerifyLock) between payment and confirmation.
// This is the "payment guard" — the defense against the slow-payment race condition.
// The guard is necessary because payment happens OUTSIDE the lock (to avoid holding
// a lock during network I/O), so the lock could expire while payment is in-flight.
public class BookingManager
{
    private readonly SeatLockManager _seatLockManager;

    public BookingManager(SeatLockManager seatLockManager)
    {
        _seatLockManager = seatLockManager;
    }

    // Standard booking with PaymentType enum (normal flow)
    public Booking? CreateBooking(User user, Show show, List<Seat> seats, PaymentType paymentType)
    {
        var processor = new PaymentProcessor();
        IPaymentMethod method = PaymentMethodFactory.Create(paymentType);
        return CreateBookingWithMethod(user, show, seats, method, processor);
    }

    // Overload accepting a custom IPaymentMethod (for testing slow/failing payments)
    public Booking? CreateBooking(User user, Show show, List<Seat> seats, IPaymentMethod paymentMethod)
    {
        var processor = new PaymentProcessor();
        return CreateBookingWithMethod(user, show, seats, paymentMethod, processor);
    }

    private Booking? CreateBookingWithMethod(User user, Show show, List<Seat> seats, IPaymentMethod method, PaymentProcessor processor)
    {
        // Step 1: Lock seats (records timestamp for expiry tracking)
        bool locked = _seatLockManager.LockSeats(show, seats, user.Id);
        if (!locked)
        {
            Console.WriteLine($"    [Booking] FAILED: Could not lock seats for {user.Name}");
            return null;
        }
        Console.WriteLine($"    [Booking] Seats locked for {user.Name}: {string.Join(", ", seats.Select(s => s.Id))}");

        // Step 2: Calculate price
        double totalAmount = show.PricingStrategy.CalculatePrice(seats);
        Console.WriteLine($"    [Booking] Total: ₹{totalAmount}");

        // Step 3: Process payment (may be slow — lock could expire during this)
        Payment payment = processor.Process(method, totalAmount);

        if (payment.Status != PaymentStatus.Success)
        {
            Console.WriteLine($"    [Booking] Payment FAILED. Unlocking seats.");
            _seatLockManager.UnlockSeats(show, seats, user.Id);
            return null;
        }

        // Step 4: Confirm seats — returns false if lock expired during payment
        bool confirmed = _seatLockManager.ConfirmSeats(show, seats, user.Id);

        if (!confirmed)
        {
            // Lock expired during payment — refund the successful payment
            Console.WriteLine($"    [Booking] FAILED: Lock expired during payment for {user.Name}. Initiating refund...");
            processor.Refund(method, payment);
            return null;
        }

        // Step 5: All good — booking confirmed
        var booking = new Booking(
            Guid.NewGuid().ToString("N")[..8],
            user, show, seats, totalAmount, payment);

        Console.WriteLine($"    [Booking] CONFIRMED! Booking ID: {booking.Id}");
        return booking;
    }
}

// ─────────────────────────────────────────────
// MovieBookingService — Singleton Facade
// ─────────────────────────────────────────────

// V3 service implements IDisposable because it owns a SeatLockManager with a background timer.
// The 'using' keyword in Main() ensures the timer is stopped when the program exits.
public class MovieBookingService : IDisposable
{
    private static MovieBookingService? _instance;
    private static readonly object _lock = new();

    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly ConcurrentDictionary<string, Movie> _movies = new();
    private readonly ConcurrentDictionary<string, Cinema> _cinemas = new();
    private readonly ConcurrentDictionary<string, City> _cities = new();
    private readonly ConcurrentDictionary<string, Show> _shows = new();

    private readonly SeatLockManager _seatLockManager;
    private readonly BookingManager _bookingManager;
    private readonly MovieSubject _movieSubject = new();

    // Lock timeout is injected to allow different values for testing vs production.
    // Short timeout (3-5s) for demos; longer (5-15 min) in real systems.
    private MovieBookingService(long lockTimeoutMs)
    {
        _seatLockManager = new SeatLockManager(lockTimeoutMs);
        _bookingManager = new BookingManager(_seatLockManager);
    }

    // Singleton with configurable timeout. The timeout is only used on first call
    // (instance creation). Subsequent calls return the existing instance regardless of parameter.
    public static MovieBookingService GetInstance(long lockTimeoutMs = 5000)
    {
        if (_instance == null)
            lock (_lock)
                _instance ??= new MovieBookingService(lockTimeoutMs);
        return _instance;
    }

    public User CreateUser(string id, string name, string email)
    {
        var user = new User(id, name, email);
        _users.TryAdd(id, user);
        return user;
    }

    public City AddCity(string id, string name)
    {
        var city = new City(id, name);
        _cities.TryAdd(id, city);
        return city;
    }

    public Cinema AddCinema(string id, string name, City city, List<Screen> screens)
    {
        var cinema = new Cinema(id, name, city, screens);
        _cinemas.TryAdd(id, cinema);
        return cinema;
    }

    public void AddMovie(Movie movie)
    {
        _movies.TryAdd(movie.Id, movie);
        _movieSubject.NotifyObservers(movie);
    }

    public Show AddShow(string id, Movie movie, Screen screen, DateTime startTime, IPricingStrategy pricing)
    {
        var show = new Show(id, movie, screen, startTime, pricing);
        _shows.TryAdd(id, show);
        return show;
    }

    public List<Show> FindShows(string movieTitle, string cityName)
    {
        var movieIds = _movies.Values
            .Where(m => m.Title.Contains(movieTitle, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Id).ToHashSet();
        var cityScreenIds = _cinemas.Values
            .Where(c => c.City.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(c => c.Screens).Select(s => s.Id).ToHashSet();
        return _shows.Values
            .Where(s => movieIds.Contains(s.Movie.Id) && cityScreenIds.Contains(s.Screen.Id)).ToList();
    }

    public Booking? BookTickets(string userId, Show show, List<Seat> seats, PaymentType paymentType)
    {
        if (!_users.TryGetValue(userId, out var user))
            throw new ArgumentException($"User '{userId}' not found");
        return _bookingManager.CreateBooking(user, show, seats, paymentType);
    }

    // Overload for custom payment methods (testing slow payments)
    public Booking? BookTickets(string userId, Show show, List<Seat> seats, IPaymentMethod paymentMethod)
    {
        if (!_users.TryGetValue(userId, out var user))
            throw new ArgumentException($"User '{userId}' not found");
        return _bookingManager.CreateBooking(user, show, seats, paymentMethod);
    }

    public void AddMovieObserver(IMovieObserver observer) => _movieSubject.AddObserver(observer);
    public void Dispose() => _seatLockManager.Dispose();
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        // Lock timeout = 3 seconds for demo purposes.
        // 'using' ensures Dispose() is called, stopping the background timer cleanly.
        using var service = MovieBookingService.GetInstance(lockTimeoutMs: 3000);

        var alice = service.CreateUser("u1", "Alice", "alice@mail.com");
        var bob = service.CreateUser("u2", "Bob", "bob@mail.com");

        var mumbai = service.AddCity("c1", "Mumbai");

        var screen1 = new Screen("scr1");
        screen1.AddSeat(new Seat("s1", 1, 1, SeatType.Regular));
        screen1.AddSeat(new Seat("s2", 1, 2, SeatType.Regular));
        screen1.AddSeat(new Seat("s3", 1, 3, SeatType.Premium));
        screen1.AddSeat(new Seat("s4", 2, 1, SeatType.Recliner));

        service.AddCinema("cin1", "PVR Phoenix", mumbai, new List<Screen> { screen1 });

        var movie = new Movie("m1", "Interstellar", 169);
        service.AddMovie(movie);

        var show = service.AddShow("sh1", movie, screen1,
            new DateTime(2025, 7, 21, 18, 0, 0), new WeekdayPricingStrategy());

        // ── Scenario 1: Fast payment — succeeds within lock timeout ──
        // Payment completes instantly, lock is still valid → booking confirmed.
        // This is the normal happy path that most users experience.
        Console.WriteLine("\n=== Scenario 1: Alice books with fast payment (succeeds) ===\n");
        var aliceBooking = service.BookTickets("u1", show,
            new List<Seat> { screen1.Seats[0], screen1.Seats[1] }, // s1, s2
            PaymentType.CreditCard);

        // ── Scenario 2: Slow payment — exceeds lock timeout ──
        // Payment takes 5 seconds but lock timeout is 3 seconds.
        // Timeline: lock at T=0 → timer expires lock at T=3 → payment returns at T=5.
        // The payment guard (VerifyLock) detects the lock is gone → booking FAILS.
        // This prevents double-booking even when payment succeeds.
        Console.WriteLine("\n=== Scenario 2: Bob books with slow payment (lock expires!) ===\n");
        Console.WriteLine("  Lock timeout: 3 seconds. Payment delay: 5 seconds.\n");
        var bobBooking = service.BookTickets("u2", show,
            new List<Seat> { screen1.Seats[2] }, // s3
            new SlowPayment(delayMs: 5000)); // 5 seconds > 3 second timeout

        // Brief sleep to let the timer's expiry log message appear in the console
        Thread.Sleep(1500);

        // ── Scenario 3: After Bob's lock expired, Alice can now book s3 ──
        // Because the timeout released Bob's lock, s3 is Available again.
        // This proves the system self-heals: abandoned/slow flows don't permanently block seats.
        Console.WriteLine("\n=== Scenario 3: Alice books s3 (available again after Bob's timeout) ===\n");
        var aliceBooking2 = service.BookTickets("u1", show,
            new List<Seat> { screen1.Seats[2] }, // s3 — was released by timeout
            PaymentType.CreditCard);

        // ── Final state — demonstrates the end result of all three scenarios ──
        Console.WriteLine("\n=== Final Seat Status ===\n");
        foreach (var seat in screen1.Seats)
            Console.WriteLine($"  {seat}");

        Console.WriteLine($"\n  Alice booking 1 (s1,s2): {(aliceBooking != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"  Bob booking (s3, slow):  {(bobBooking != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"  Alice booking 2 (s3):    {(aliceBooking2 != null ? "SUCCESS" : "FAILED")}");
    }
}
