using System.Collections.Concurrent;
using System.Collections.Immutable;

// Movie Ticket Booking System V2 — Fully Thread-Safe
//
// V1 Thread-Safety Gaps:
//   1. Seat.Status is a mutable property — read/written from multiple threads without sync
//   2. Screen.Seats (List) — no protection during concurrent iteration + modification
//   3. MovieSubject._observers (List) — AddObserver during NotifyObservers → crash
//   4. Booking.ConfirmBooking() changes seat status outside the SeatLockManager's lock
//   5. SeatLockManager uses one global lock for all shows (correct but poor throughput)
//
// V2 Fixes:
//   1. Seat.Status changes only happen inside SeatLockManager's per-show lock
//      → ConfirmBooking delegates back to SeatLockManager.ConfirmSeats()
//   2. Screen.Seats uses ImmutableList — thread-safe iteration
//   3. MovieSubject uses ImmutableList<IMovieObserver> with ImmutableInterlocked
//   4. SeatLockManager uses per-show locks (ConcurrentDictionary<Show, object>)
//      → Two bookings for DIFFERENT shows proceed in parallel
//      → Two bookings for the SAME show are serialized
//   5. All seat status transitions are centralized in SeatLockManager

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────

// Same enums as V1 — the domain model doesn't change, only synchronization does.
public enum SeatType
{
    Regular,
    Premium,
    Recliner
}

public enum SeatStatus
{
    Available,
    Locked,
    Booked
}

public enum PaymentStatus
{
    Pending,
    Success,
    Failure
}

// ─────────────────────────────────────────────
// Models
// ─────────────────────────────────────────────

// Immutable value objects — no thread-safety concerns for these.
public class User
{
    public string Id { get; }
    public string Name { get; }
    public string Email { get; }

    public User(string id, string name, string email)
    {
        Id = id;
        Name = name;
        Email = email;
    }
}

public class Movie
{
    public string Id { get; }
    public string Title { get; }
    public int DurationMinutes { get; }

    public Movie(string id, string title, int durationMinutes)
    {
        Id = id;
        Title = title;
        DurationMinutes = durationMinutes;
    }
}

public class City
{
    public string Id { get; }
    public string Name { get; }

    public City(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

// V2: Seat status is ONLY changed through SeatLockManager (centralized transitions).
// In V1, Seat.Status had a public setter — any code could change it, creating race conditions.
// V2 makes the setter internal and routes all transitions through the lock manager,
// ensuring every status change happens under the appropriate per-show lock.
public class Seat
{
    public string Id { get; }
    public int Row { get; }
    public int Col { get; }
    public SeatType Type { get; }

    // volatile ensures that reads from other threads always see the latest write.
    // Without volatile, a thread could cache a stale value (CPU register/cache optimization).
    // This is necessary because status is read outside locks (e.g., in UI display)
    // but written inside locks (in SeatLockManager).
    private volatile SeatStatus _status;
    public SeatStatus Status => _status;

    // Internal setter — only SeatLockManager should call this.
    // Using 'internal' access modifier restricts mutation to within the assembly,
    // preventing external consumers from bypassing the lock manager.
    internal void SetStatus(SeatStatus status)
    {
        _status = status;
    } 

    public Seat(string id, int row, int col, SeatType type)
    {
        Id = id;
        Row = row;
        Col = col;
        Type = type;
        _status = SeatStatus.Available;
    }

    public override string ToString() => $"Seat({Id}, R{Row}C{Col}, {Type}, {Status})";
}

// V2: Screen uses ImmutableList for thread-safe iteration.
// In V1, if one thread adds a seat while another iterates Screen.Seats, you get
// InvalidOperationException ("Collection was modified during enumeration").
// ImmutableList solves this: AddSeat creates a new list, and existing iterators
// continue on the old snapshot — no locking needed for reads.
public class Screen
{
    public string Id { get; }
    private ImmutableList<Seat> _seats = ImmutableList<Seat>.Empty;

    // Returns the current snapshot — safe to iterate even if AddSeat is called concurrently.
    public ImmutableList<Seat> Seats => _seats;

    public Screen(string id) => Id = id;

    // ImmutableInterlocked.Update atomically replaces the list reference.
    // It uses a compare-and-swap loop: read current → compute new → CAS.
    // If another thread modified _seats between read and CAS, it retries.
    public void AddSeat(Seat seat)
    {
        ImmutableInterlocked.Update(ref _seats, list => list.Add(seat));
    }
}

// Cinema is created once with its screens — no thread-safety concern for reads.
public class Cinema
{
    public string Id { get; }
    public string Name { get; }
    public City City { get; }
    public List<Screen> Screens { get; }

    public Cinema(string id, string name, City city, List<Screen> screens)
    {
        Id = id;
        Name = name;
        City = city;
        Screens = screens;
    }
}

// Show is immutable after creation — binds movie, screen, time, and pricing together.
public class Show
{
    public string Id { get; }
    public Movie Movie { get; }
    public Screen Screen { get; }
    public DateTime StartTime { get; }
    public IPricingStrategy PricingStrategy { get; }

    public Show(string id, Movie movie, Screen screen, DateTime startTime, IPricingStrategy pricingStrategy)
    {
        Id = id;
        Movie = movie;
        Screen = screen;
        StartTime = startTime;
        PricingStrategy = pricingStrategy;
    }
}

// Payment is a short-lived object created during booking — not shared across threads.
public class Payment
{
    public string Id { get; }
    public double Amount { get; }
    public PaymentStatus Status { get; set; }
    public string TransactionId { get; set; } = string.Empty;

    public Payment(string id, double amount)
    {
        Id = id;
        Amount = amount;
        Status = PaymentStatus.Pending;
    }
}

// V2: Booking no longer modifies seat status directly.
// In V1, ConfirmBooking() set seat.Status = Booked outside any lock — a race condition.
// V2 removes that method entirely. Seat confirmation is handled by SeatLockManager.ConfirmSeats()
// which runs under the per-show lock, ensuring atomicity.
public class Booking
{
    public string Id { get; }
    public User User { get; }
    public Show Show { get; }
    public List<Seat> Seats { get; }
    public double TotalAmount { get; }
    public Payment Payment { get; }

    public Booking(string id, User user, Show show, List<Seat> seats, double totalAmount, Payment payment)
    {
        Id = id;
        User = user;
        Show = show;
        Seats = seats;
        TotalAmount = totalAmount;
        Payment = payment;
    }

    // V2: No longer changes seat status directly.
    // Confirmation is done through SeatLockManager.ConfirmSeats()
}

// ─────────────────────────────────────────────
// Strategy: Pricing
// ─────────────────────────────────────────────

// Pricing strategies are stateless and thread-safe by nature — no mutable state.
// Multiple threads can call CalculatePrice concurrently without any issues.
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
            if (seat.Type == SeatType.Premium)
                total += 350;
            else if (seat.Type == SeatType.Recliner)
                total += 500;
            else
                total += 200;
        }
        return total;
    }
}

public class WeekendPricingStrategy : IPricingStrategy
{
    private const double WeekendSurcharge = 1.5;

    public double CalculatePrice(List<Seat> seats)
    {
        double total = 0;
        foreach (var seat in seats)
        {
            if (seat.Type == SeatType.Premium)
                total += 350 * WeekendSurcharge;
            else if (seat.Type == SeatType.Recliner)
                total += 500 * WeekendSurcharge;
            else
                total += 200 * WeekendSurcharge;
        }
        return total;
    }
}

// ─────────────────────────────────────────────
// Payment: PaymentType + IPaymentMethod + Factory + Processor
// ─────────────────────────────────────────────

// All payment classes are inherently thread-safe — they create new Payment objects
// per invocation and don't share mutable state.

public enum PaymentType
{
    CreditCard,
    UPI,
    Wallet
}

public interface IPaymentMethod
{
    Payment Pay(double amount);
}

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
}

public class PaymentProcessor
{
    public Payment Process(PaymentType type, double amount)
    {
        IPaymentMethod method = PaymentMethodFactory.Create(type);
        return method.Pay(amount);
    }
}

// ─────────────────────────────────────────────
// Observer: Movie notifications (thread-safe with ImmutableList)
// ─────────────────────────────────────────────

public interface IMovieObserver
{
    void Update(Movie movie);
}

public class UserObserver : IMovieObserver
{
    private readonly User _user;
    public UserObserver(User user) => _user = user;

    public void Update(Movie movie)
    {
        Console.WriteLine($"    [Notify] {_user.Name}: New movie added - \"{movie.Title}\"");
    }
}

// V2: MovieSubject uses ImmutableList to solve the concurrent modification problem.
// In V1, calling AddObserver while NotifyObservers is iterating throws an exception.
// With ImmutableList, AddObserver atomically replaces the list reference.
// NotifyObservers iterates a snapshot — even if a new observer is added mid-iteration,
// the current loop continues safely on the old list. The new observer will be
// included in the NEXT notification.
public class MovieSubject
{
    private ImmutableList<IMovieObserver> _observers = ImmutableList<IMovieObserver>.Empty;

    // ImmutableInterlocked.Update uses CAS (compare-and-swap) to atomically replace the list.
    // No lock needed — lock-free thread safety via immutable data structures.
    public void AddObserver(IMovieObserver observer)
    {
        ImmutableInterlocked.Update(ref _observers, list => list.Add(observer));
    }

    public void RemoveObserver(IMovieObserver observer)
    {
        ImmutableInterlocked.Update(ref _observers, list => list.Remove(observer));
    }

    // Safe to iterate — _observers is an immutable snapshot at the time of read.
    // Even if AddObserver is called concurrently, this foreach sees a consistent list.
    public void NotifyObservers(Movie movie)
    {
        foreach (var observer in _observers)
            observer.Update(movie);
    }
}

// ─────────────────────────────────────────────
// SeatLockManager — per-show locks, centralized seat state transitions
// ─────────────────────────────────────────────

// V2 SeatLockManager is the single authority for all seat state transitions.
// Key improvement over V1: per-show locks instead of one global lock.
// This means two users booking for DIFFERENT shows run in parallel (no contention),
// while two users booking the SAME show are properly serialized.
// All seat status changes (Available→Locked, Locked→Booked, Locked→Available) happen here.
public class SeatLockManager
{
    // Per-show lock objects — each show gets its own monitor.
    // ConcurrentDictionary ensures thread-safe creation of lock objects.
    // Using show.Id (string) as key instead of Show object avoids reference equality issues.
    private readonly ConcurrentDictionary<string, object> _showLocks = new();

    // Tracks which seats are locked for each show: showId → (seatId → userId).
    // Using string IDs (not object references) ensures consistent lookups across threads.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _lockedSeats = new();

    // Returns (or creates) the lock object for a specific show.
    // GetOrAdd is atomic — only one lock object is created per show regardless of concurrency.
    private object GetShowLock(Show show)
    {
        return _showLocks.GetOrAdd(show.Id, _ => new object());
    }

    // Lock seats for a user. All-or-nothing: either all lock or none do.
    // The per-show lock means this only blocks other bookings for the SAME show.
    public bool LockSeats(Show show, List<Seat> seats, string userId)
    {
        var showLock = GetShowLock(show);

        lock (showLock) // Per-show lock — other shows proceed in parallel
        {
            var seatLocks = _lockedSeats.GetOrAdd(show.Id, _ => new ConcurrentDictionary<string, string>());

            // Validate all seats are available — check both the seat's status
            // and our tracking map for consistency
            foreach (var seat in seats)
            {
                if (seat.Status != SeatStatus.Available)
                    return false;
                if (seatLocks.ContainsKey(seat.Id))
                    return false;
            }

            // Lock all atomically — since we hold the per-show lock, no other thread
            // can interleave between validation and mutation for this show
            foreach (var seat in seats)
            {
                seat.SetStatus(SeatStatus.Locked);
                seatLocks.TryAdd(seat.Id, userId);
            }
            return true;
        }
    }

    // Confirm seats (Locked → Booked). Called after successful payment.
    // This is the V2 fix for V1's ConfirmBooking() race condition.
    // By running under the per-show lock, we guarantee no other thread can
    // see a partially-confirmed booking (some seats Booked, others still Locked).
    public void ConfirmSeats(Show show, List<Seat> seats, string userId)
    {
        var showLock = GetShowLock(show);

        lock (showLock)
        {
            if (!_lockedSeats.TryGetValue(show.Id, out var seatLocks)) 
                return;

            foreach (var seat in seats)
            {
                // Only confirm if this user still holds the lock — defensive check
                if (seatLocks.TryGetValue(seat.Id, out var lockedBy) && lockedBy == userId)
                {
                    seat.SetStatus(SeatStatus.Booked);
                    // Remove from lock tracking — booked seats don't need lock entries
                    seatLocks.TryRemove(seat.Id, out _);
                }
            }
        }
    }

    // Unlock seats (Locked → Available). Called on payment failure or timeout.
    // Same per-show lock ensures this doesn't race with LockSeats or ConfirmSeats.
    public void UnlockSeats(Show show, List<Seat> seats, string userId)
    {
        var showLock = GetShowLock(show);

        lock (showLock)
        {
            if (!_lockedSeats.TryGetValue(show.Id, out var seatLocks)) return;

            foreach (var seat in seats)
            {
                // Only unlock if this user holds the lock — prevents one user from
                // releasing another user's locked seats
                if (seatLocks.TryGetValue(seat.Id, out var lockedBy) && lockedBy == userId)
                {
                    seat.SetStatus(SeatStatus.Available);
                    seatLocks.TryRemove(seat.Id, out _);
                }
            }
        }
    }
}

// ─────────────────────────────────────────────
// BookingManager — orchestrates booking flow
// ─────────────────────────────────────────────

// BookingManager coordinates the booking steps. It's stateless (no mutable fields),
// so it's inherently thread-safe — multiple threads can call CreateBooking concurrently.
// Thread safety for seat state is delegated entirely to SeatLockManager.
public class BookingManager
{
    private readonly SeatLockManager _seatLockManager;

    public BookingManager(SeatLockManager seatLockManager)
    {
        _seatLockManager = seatLockManager;
    }

    public Booking? CreateBooking(User user, Show show, List<Seat> seats, PaymentType paymentType)
    {
        // Step 1: Lock seats — per-show lock serializes concurrent attempts
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

        // Step 3: Process payment via PaymentProcessor (resolves from PaymentType enum)
        var processor = new PaymentProcessor();
        Payment payment = processor.Process(paymentType, totalAmount);

        if (payment.Status != PaymentStatus.Success)
        {
            Console.WriteLine($"    [Booking] Payment FAILED. Unlocking seats.");
            _seatLockManager.UnlockSeats(show, seats, user.Id);
            return null;
        }

        // Step 4: Confirm seats (Locked → Booked) through SeatLockManager
        _seatLockManager.ConfirmSeats(show, seats, user.Id);

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

// Same Singleton Facade as V1, but with thread-safe internals.
// All entity stores use ConcurrentDictionary for lock-free reads and atomic writes.
public class MovieBookingService
{
    private static MovieBookingService? _instance;
    private static readonly object _lock = new();

    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly ConcurrentDictionary<string, Movie> _movies = new();
    private readonly ConcurrentDictionary<string, Cinema> _cinemas = new();
    private readonly ConcurrentDictionary<string, City> _cities = new();
    private readonly ConcurrentDictionary<string, Show> _shows = new();

    private readonly SeatLockManager _seatLockManager = new();
    private readonly BookingManager _bookingManager;
    private readonly MovieSubject _movieSubject = new();

    private MovieBookingService()
    {
        _bookingManager = new BookingManager(_seatLockManager);
    }

    // V2: Explicit double-checked locking with full if-block syntax for clarity.
    // The outer null check avoids lock acquisition on every call after initialization.
    // The inner null check (inside lock) handles the case where two threads both pass
    // the outer check simultaneously — only one creates the instance.
    public static MovieBookingService GetInstance()
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                if(_instance == null)
                {
                    _instance = new MovieBookingService();
                }
            }
        }                
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
        // Observer notification is thread-safe thanks to ImmutableList in MovieSubject
        _movieSubject.NotifyObservers(movie);
    }

    public Show AddShow(string id, Movie movie, Screen screen, DateTime startTime, IPricingStrategy pricingStrategy)
    {
        var show = new Show(id, movie, screen, startTime, pricingStrategy);
        _shows.TryAdd(id, show);
        return show;
    }

    // FindShows iterates ConcurrentDictionary.Values — safe even during concurrent writes.
    // LINQ operations on ConcurrentDictionary produce a snapshot of the values at read time.
    public List<Show> FindShows(string movieTitle, string cityName)
    {
        var movieIds = _movies.Values
            .Where(m => m.Title.Contains(movieTitle, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Id)
            .ToHashSet();

        var cityScreenIds = _cinemas.Values
            .Where(c => c.City.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(c => c.Screens)
            .Select(s => s.Id)
            .ToHashSet();

        return _shows.Values
            .Where(s => movieIds.Contains(s.Movie.Id) && cityScreenIds.Contains(s.Screen.Id))
            .ToList();
    }

    public Cinema? FindCinemaForShow(Show show)
    {
        return _cinemas.Values.FirstOrDefault(c => c.Screens.Any(s => s.Id == show.Screen.Id));
    }

    public Booking? BookTickets(string userId, Show show, List<Seat> seats, PaymentType paymentType)
    {
        if (!_users.TryGetValue(userId, out var user))
            throw new ArgumentException($"User '{userId}' not found");

        return _bookingManager.CreateBooking(user, show, seats, paymentType);
    }

    public void AddMovieObserver(IMovieObserver observer) => _movieSubject.AddObserver(observer);
}

// ─────────────────────────────────────────────
// Demo — includes concurrent booking to prove thread-safety
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var service = MovieBookingService.GetInstance();

        // Setup
        var alice = service.CreateUser("u1", "Alice", "alice@mail.com");
        var bob = service.CreateUser("u2", "Bob", "bob@mail.com");
        var charlie = service.CreateUser("u3", "Charlie", "charlie@mail.com");

        service.AddMovieObserver(new UserObserver(alice));
        service.AddMovieObserver(new UserObserver(bob));

        var mumbai = service.AddCity("c1", "Mumbai");

        var screen1 = new Screen("scr1");
        screen1.AddSeat(new Seat("s1", 1, 1, SeatType.Regular));
        screen1.AddSeat(new Seat("s2", 1, 2, SeatType.Regular));
        screen1.AddSeat(new Seat("s3", 1, 3, SeatType.Premium));
        screen1.AddSeat(new Seat("s4", 2, 1, SeatType.Premium));
        screen1.AddSeat(new Seat("s5", 2, 2, SeatType.Recliner));
        screen1.AddSeat(new Seat("s6", 2, 3, SeatType.Recliner));

        service.AddCinema("cin1", "PVR Phoenix", mumbai, new List<Screen> { screen1 });

        Console.WriteLine("=== Adding Movie ===\n");
        var movie = new Movie("m1", "Interstellar", 169);
        service.AddMovie(movie);

        var show = service.AddShow("sh1", movie, screen1,
            new DateTime(2025, 7, 21, 18, 0, 0), new WeekdayPricingStrategy());

        // ── Concurrent Booking: Alice and Bob race for the same seats ──
        // This is the key V2 test: two threads attempt to lock the same seats simultaneously.
        // The per-show lock ensures exactly one succeeds — no double-booking.
        Console.WriteLine("\n=== Concurrent Booking Race (Alice vs Bob for same seats) ===\n");

        var targetSeats = new List<Seat> { screen1.Seats[0], screen1.Seats[1] }; // s1, s2

        Booking? aliceBooking = null;
        Booking? bobBooking = null;

        // Task.Run schedules work on the thread pool — true parallelism on multi-core CPUs.
        // Both tasks hit LockSeats at roughly the same time, proving the lock works.
        var aliceTask = Task.Run(() =>
        {
            aliceBooking = service.BookTickets("u1", show, targetSeats, PaymentType.CreditCard);
        });

        var bobTask = Task.Run(() =>
        {
            bobBooking = service.BookTickets("u2", show, targetSeats, PaymentType.UPI);
        });

        // Wait for both to complete — one will have succeeded, one will have failed
        Task.WaitAll(aliceTask, bobTask);

        Console.WriteLine($"\n  Alice booking: {(aliceBooking != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"  Bob booking:   {(bobBooking != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"  Exactly one should succeed (no double-booking).");

        // ── Concurrent Bookings for DIFFERENT seats (both should succeed) ──
        // This demonstrates that per-show locks don't over-serialize.
        // Different seats within the same show can be booked concurrently because
        // the lock validates individual seat availability, not the entire screen.
        // (Though they still serialize on the per-show lock, the second thread succeeds
        // because it requests different seats that are still available.)
        Console.WriteLine("\n=== Concurrent Bookings for Different Seats (both succeed) ===\n");

        var charlieSeats = new List<Seat> { screen1.Seats[2] }; // s3 Premium
        var bobSeats2 = new List<Seat> { screen1.Seats[4] };    // s5 Recliner

        Booking? charlieBooking = null;
        Booking? bobBooking2 = null;

        var charlieTask = Task.Run(() =>
        {
            charlieBooking = service.BookTickets("u3", show, charlieSeats, PaymentType.Wallet);
        });

        var bobTask2 = Task.Run(() =>
        {
            bobBooking2 = service.BookTickets("u2", show, bobSeats2, PaymentType.CreditCard);
        });

        Task.WaitAll(charlieTask, bobTask2);

        Console.WriteLine($"\n  Charlie booking (s3): {(charlieBooking != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"  Bob booking (s5):     {(bobBooking2 != null ? "SUCCESS" : "FAILED")}");

        // ── Final seat status — shows the end state after all concurrent operations ──
        Console.WriteLine("\n=== Final Seat Status ===\n");
        foreach (var seat in screen1.Seats)
        {
            Console.WriteLine($"  {seat}");
        }
    }
}
