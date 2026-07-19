using System.Collections.Concurrent;

// Movie Ticket Booking System V1
//
// Core Entities:
//   User              - id, name, email
//   Movie             - id, title, durationMinutes
//   City              - id, name
//   Cinema            - id, name, city, list of screens
//   Screen            - id, list of seats
//   Seat              - id, row, col, type (Regular/Premium/Recliner), status (Available/Locked/Booked)
//   Show              - id, movie, screen, startTime, pricingStrategy
//   Booking           - id, user, show, seats, totalAmount, payment
//   Payment           - id, amount, status (Pending/Success/Failure), transactionId
//   SeatLockManager   - locks seats temporarily to prevent double-booking
//   BookingManager    - orchestrates booking: lock seats → pay → confirm
//   PricingStrategy   - interface for calculating ticket price (Weekday/Weekend)
//   PaymentStrategy   - interface for payment (CreditCard, etc.)
//   MovieSubject/Observer - Observer pattern: notify users when new movies are added
//   MovieBookingService - Singleton facade: manages all entities and exposes simple API
//
// Design Patterns Used:
//   - Singleton: MovieBookingService (single coordination point)
//   - Strategy: PricingStrategy, PaymentStrategy (pluggable algorithms)
//   - Observer: MovieSubject/MovieObserver (notify on new movies)
//   - Facade: MovieBookingService hides complexity behind a simple API
//
// Overall Flow:
//   1. Admin adds cities, cinemas, screens, movies, shows
//   2. User searches for shows by movie + city
//   3. User selects seats → SeatLockManager locks them (temporary)
//   4. BookingManager creates booking → calculates price → processes payment
//   5. On success → seats marked BOOKED, booking confirmed
//   6. On failure → seats unlocked (returned to AVAILABLE)

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────

// SeatType determines pricing tier — each type has a different base price.
// This allows the pricing strategy to apply different rates per category.
public enum SeatType
{
    Regular,
    Premium,
    Recliner
}

// SeatStatus tracks the lifecycle of a seat for a given show.
// Available → Locked (temporary hold during payment) → Booked (confirmed).
// This state machine prevents double-booking by making locked seats unavailable to others.
public enum SeatStatus
{
    Available,
    Locked,
    Booked
}

// PaymentStatus drives the booking outcome — only Success leads to confirmation.
// Pending is the initial state before the payment gateway responds.
public enum PaymentStatus
{
    Pending,
    Success,
    Failure
}

// ─────────────────────────────────────────────
// Models
// ─────────────────────────────────────────────

// User is an immutable value object — once created, identity doesn't change.
// Immutability here simplifies reasoning: no need to worry about concurrent mutations.
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

// Movie is also immutable — movie metadata doesn't change after creation.
// Separating Movie from Show allows the same movie to be shown at different times/screens.
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

// City exists as a separate entity to enable multi-city search.
// Users filter shows by city, so we need a first-class City model to group cinemas geographically.
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

// Seat is the only mutable model — its Status changes during the booking lifecycle.
// This mutability is the source of concurrency concerns addressed in V2.
// Row/Col identify physical position; Type determines pricing tier.
public class Seat
{
    public string Id { get; }
    public int Row { get; }
    public int Col { get; }
    public SeatType Type { get; }
    public SeatStatus Status { get; set; } // Mutable: Available → Locked → Booked

    public Seat(string id, int row, int col, SeatType type)
    {
        Id = id;
        Row = row;
        Col = col;
        Type = type;
        Status = SeatStatus.Available;
    }

    public override string ToString() => $"Seat({Id}, R{Row}C{Col}, {Type}, {Status})";
}

// Screen represents a physical auditorium. It owns a list of Seats.
// The relationship is: Cinema has many Screens, Screen has many Seats.
// A Show is assigned to a specific Screen, which determines available seats.
public class Screen
{
    public string Id { get; }
    public List<Seat> Seats { get; } = new();       // A screen has a list of seats

    public Screen(string id) 
    {
        Id = id;
    }
    public void AddSeat(Seat seat)
    {
        Seats.Add(seat);
    }
}

// Cinema groups Screens under a named venue in a specific City.
// This hierarchy (City → Cinema → Screen → Seat) enables location-based searching.
public class Cinema
{
    public string Id { get; }
    public string Name { get; }
    public City City { get; }
    public List<Screen> Screens { get; }            // A Cinema/Theatre has a list of Screens

    public Cinema(string id, string name, City city, List<Screen> screens)
    {
        Id = id;
        Name = name;
        City = city;
        Screens = screens;
    }
}

// Show binds a Movie to a Screen at a specific time with a pricing strategy.
// The PricingStrategy is attached here (not to Cinema or Movie) because pricing
// can vary by showtime — e.g., weekday vs weekend shows have different rates.
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

// Payment is a record of a financial transaction.
// Status starts as Pending and transitions to Success/Failure after gateway response.
// TransactionId is assigned by the payment gateway on success for audit/refund purposes.
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

// Booking ties together the user, show, selected seats, and payment.
// It represents a confirmed reservation. ConfirmBooking() finalizes seat status.
// This is the "aggregate" in domain terms — the unit of consistency for a ticket purchase.
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

    // Marks all seats as Booked. In V1 this happens outside the SeatLockManager's lock,
    // which is a thread-safety gap — two threads could interleave here. Fixed in V2.
    public void ConfirmBooking()
    {
        foreach (var seat in Seats)
            seat.Status = SeatStatus.Booked;
    }
}

// ─────────────────────────────────────────────
// Strategy: Pricing
// ─────────────────────────────────────────────

// IPricingStrategy uses the Strategy pattern to decouple price calculation from the booking flow.
// This allows new pricing rules (e.g., holiday, matinee) to be added without modifying BookingManager.
public interface IPricingStrategy
{
    double CalculatePrice(List<Seat> seats);
}

// Weekday pricing uses base rates per seat type.
// Keeping rates in the strategy (not in Seat) means the same seat can have different prices
// depending on when the show is scheduled.
public class WeekdayPricingStrategy : IPricingStrategy
{
    public double CalculatePrice(List<Seat> seats)
    {
        double total = 0;
        foreach (var seat in seats)
        {
            // Price tiering by seat type — Regular is cheapest, Recliner is premium experience
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

// Weekend pricing applies a multiplier on top of base rates.
// Using a const multiplier (not hardcoded per line) makes it easy to adjust the surcharge.
public class WeekendPricingStrategy : IPricingStrategy
{
    private const double WeekendSurcharge = 1.5;

    public double CalculatePrice(List<Seat> seats)
    {
        double total = 0;
        foreach (var seat in seats)
        {
            // Same base prices as weekday, but scaled by the weekend surcharge factor
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

// PaymentType is what the user selects — they don't construct payment objects.
// The factory resolves this enum into the correct IPaymentMethod implementation.
public enum PaymentType
{
    CreditCard,
    UPI,
    Wallet
}

// IPaymentMethod is the interface that every payment implementation must satisfy.
// Each implementation knows how to process a payment for its specific channel.
public interface IPaymentMethod
{
    Payment Pay(double amount);
}

// Simulates a credit card charge via an external payment gateway.
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

// Simulates a UPI payment (e.g., Google Pay, PhonePe).
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

// Simulates a wallet deduction (e.g., Paytm, Amazon Pay).
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

// Factory that maps PaymentType enum → IPaymentMethod instance.
// Adding a new payment method: add enum value + add case here + implement IPaymentMethod.
// No changes needed in BookingManager or anywhere else in the system.
public static class PaymentMethodFactory
{
    public static IPaymentMethod Create(PaymentType type)
    {
        if (type == PaymentType.CreditCard)
            return new CreditCardPayment();
        else if (type == PaymentType.UPI)
            return new UPIPayment();
        else if (type == PaymentType.Wallet)
            return new WalletPayment();
        else
            throw new ArgumentException($"Unknown payment type: {type}");
    }
}

// PaymentProcessor is the orchestrator — takes a PaymentType, resolves the method via factory, calls Pay().
// The caller only passes an enum (simple), the processor handles the rest.
// This hides construction complexity: API keys, gateway URLs, retry configs would be in the factory.
public class PaymentProcessor
{
    public Payment Process(PaymentType type, double amount)
    {
        IPaymentMethod method = PaymentMethodFactory.Create(type);
        return method.Pay(amount);
    }
}

// ─────────────────────────────────────────────
// Observer: Movie notifications
// ─────────────────────────────────────────────

// IMovieObserver is the Observer interface — objects that want to be notified
// when new movies are added implement this. Decouples notification logic from movie creation.
public interface IMovieObserver
{
    void Update(Movie movie);
}

// UserObserver wraps a User and prints a notification when a new movie is added.
// In production, this might send an email, push notification, or SMS.
public class UserObserver : IMovieObserver
{
    private readonly User _user;

    public UserObserver(User user) => _user = user;

    public void Update(Movie movie)
    {
        Console.WriteLine($"    [Notify] {_user.Name}: New movie added - \"{movie.Title}\"");
    }
}

// MovieSubject is the Subject in the Observer pattern.
// It maintains a list of observers and notifies all of them when a movie is added.
// V1 gap: using a plain List here is not thread-safe — adding an observer while
// iterating (NotifyObservers) can cause a ConcurrentModificationException. Fixed in V2.
public class MovieSubject
{
    private readonly List<IMovieObserver> _observers = new();

    public void AddObserver(IMovieObserver observer) => _observers.Add(observer);
    public void RemoveObserver(IMovieObserver observer) => _observers.Remove(observer);

    public void NotifyObservers(Movie movie)
    {
        foreach (var observer in _observers)
            observer.Update(movie);
    }
}

// ─────────────────────────────────────────────
// SeatLockManager — prevents double-booking with temp locks
// ─────────────────────────────────────────────

// SeatLockManager is the critical component that prevents two users from booking the same seat.
// It uses a temporary lock: when a user selects seats, they're "held" while payment processes.
// If payment fails or times out, the lock is released so others can book those seats.
// V1 uses a single global lock (_lock) for all shows — correct but limits throughput.
public class SeatLockManager
{
    private const long LockTimeoutMs = 300_000; // 5 minutes — in production, would be configurable

    // Two-level map: Show → (Seat → UserId). Tracks who locked which seat for which show.
    // ConcurrentDictionary used for the outer map, but actual synchronization is via _lock.
    private readonly ConcurrentDictionary<Show, ConcurrentDictionary<Seat, string>> _lockedSeats = new();

    // Single global lock — simple but means all shows are serialized.
    // This is a throughput bottleneck: booking for Show A blocks booking for Show B.
    private readonly object _lock = new();

    // Lock seats for a user. Returns true if all seats locked successfully.
    // Uses all-or-nothing semantics: either ALL requested seats are locked, or NONE are.
    // This prevents partial locks where a user holds some seats but can't get the rest.
    public bool LockSeats(Show show, List<Seat> seats, string userId)
    {
        lock (_lock)
        {
            var showLocks = _lockedSeats.GetOrAdd(show, _ => new ConcurrentDictionary<Seat, string>());

            // First pass: validate ALL seats are available before locking any.
            // This check-then-act is safe because we hold the lock for the entire operation.
            foreach (var seat in seats)
            {
                if (seat.Status != SeatStatus.Available)
                    return false; // Already locked or booked by someone
                if (showLocks.ContainsKey(seat))
                    return false; // Already locked in our tracking map
            }

            // Second pass: lock all seats atomically.
            // Both the seat's Status and our tracking map are updated together.
            foreach (var seat in seats)
            {
                seat.Status = SeatStatus.Locked;
                showLocks.TryAdd(seat, userId);
            }
            return true;
        }
    }

    // Unlock seats — called when payment fails or lock times out.
    // Only the user who locked the seats can unlock them (userId check prevents hijacking).
    public void UnlockSeats(Show show, List<Seat> seats, string userId)
    {
        lock (_lock)
        {
            if (!_lockedSeats.TryGetValue(show, out var showLocks)) return;

            foreach (var seat in seats)
            {
                // Verify this user actually holds the lock before releasing
                if (showLocks.TryGetValue(seat, out var lockedBy) && lockedBy == userId)
                {
                    seat.Status = SeatStatus.Available;
                    showLocks.TryRemove(seat, out _);
                }
            }
        }
    }
}

// ─────────────────────────────────────────────
// BookingManager — orchestrates the booking flow
// ─────────────────────────────────────────────

// BookingManager coordinates the multi-step booking process: lock → price → pay → confirm.
// It doesn't own the lock logic (SeatLockManager does) — separation of concerns.
// This class is stateless beyond its dependency, making it easy to test in isolation.
public class BookingManager
{
    private readonly SeatLockManager _seatLockManager;

    public BookingManager(SeatLockManager seatLockManager)
    {
        _seatLockManager = seatLockManager;
    }

    // Main booking flow: lock → calculate price → pay → confirm (or unlock on failure).
    // Returns null if booking fails at any step (lock contention or payment failure).
    public Booking? CreateBooking(User user, Show show, List<Seat> seats, PaymentType paymentType)
    {
        // Step 1: Lock seats — prevents others from booking them while we process payment.
        bool locked = _seatLockManager.LockSeats(show, seats, user.Id);
        if (!locked)
        {
            Console.WriteLine($"    [Booking] FAILED: Could not lock seats for {user.Name}");
            return null;
        }
        Console.WriteLine($"    [Booking] Seats locked for {user.Name}: {string.Join(", ", seats.Select(s => s.Id))}");

        // Step 2: Calculate price using the show's attached pricing strategy.
        double totalAmount = show.PricingStrategy.CalculatePrice(seats);
        Console.WriteLine($"    [Booking] Total: ₹{totalAmount}");

        // Step 3: Process payment via PaymentProcessor (resolves method from PaymentType).
        var processor = new PaymentProcessor();
        Payment payment = processor.Process(paymentType, totalAmount);

        if (payment.Status != PaymentStatus.Success)
        {
            Console.WriteLine($"    [Booking] Payment FAILED. Unlocking seats.");
            _seatLockManager.UnlockSeats(show, seats, user.Id);
            return null;
        }

        // Step 4: Payment succeeded — create the booking and confirm seats.
        var booking = new Booking(
            Guid.NewGuid().ToString("N")[..8],
            user, show, seats, totalAmount, payment);

        booking.ConfirmBooking();
        Console.WriteLine($"    [Booking] CONFIRMED! Booking ID: {booking.Id}");
        return booking;
    }
}

// ─────────────────────────────────────────────
// MovieBookingService — Singleton Facade
// ─────────────────────────────────────────────

// MovieBookingService is the single entry point for all operations (Facade pattern).
// Singleton ensures there's one coordinated instance managing all state — prevents
// inconsistencies that would arise from multiple instances with separate lock managers.
// ConcurrentDictionary is used for all collections because this service may be accessed
// from multiple threads (e.g., web request handlers), even though V1 doesn't fully
// protect all operations.
public class MovieBookingService
{
    private static MovieBookingService? _instance;
    private static readonly object _lock = new();

    // All entity stores use ConcurrentDictionary for basic thread-safe reads/writes.
    // However, compound operations (read-then-write) still need external synchronization.
    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly ConcurrentDictionary<string, Movie> _movies = new();
    private readonly ConcurrentDictionary<string, Cinema> _cinemas = new();
    private readonly ConcurrentDictionary<string, City> _cities = new();
    private readonly ConcurrentDictionary<string, Show> _shows = new();

    private readonly SeatLockManager _seatLockManager = new();
    private readonly BookingManager _bookingManager;
    private readonly MovieSubject _movieSubject = new();

    // Private constructor enforces Singleton — no external code can create a second instance.
    private MovieBookingService()
    {
        _bookingManager = new BookingManager(_seatLockManager);
    }

    // Double-checked locking pattern for thread-safe lazy initialization.
    // First check avoids lock overhead on subsequent calls after initialization.
    // The lock + null-coalescing assignment ensures only one thread creates the instance.
    public static MovieBookingService GetInstance()
    {
        if (_instance == null)
            lock (_lock)
                _instance ??= new MovieBookingService();
        return _instance;
    }

    // ── Admin operations ──

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

    // Adding a movie triggers observer notifications — decouples "movie added" event
    // from "who needs to know about it". New notification channels can subscribe
    // without modifying this method.
    public void AddMovie(Movie movie)
    {
        _movies.TryAdd(movie.Id, movie);
        _movieSubject.NotifyObservers(movie); // Observer: notify all subscribers
    }

    public Show AddShow(string id, Movie movie, Screen screen, DateTime startTime, IPricingStrategy pricingStrategy)
    {
        var show = new Show(id, movie, screen, startTime, pricingStrategy);
        _shows.TryAdd(id, show);
        return show;
    }

    // ── User operations ──

    // Search shows by movie title and city name.
    // This two-step filter (find matching movies, find screens in city, intersect on shows)
    // mimics how a real booking app works: user picks a movie and their city.
    public List<Show> FindShows(string movieTitle, string cityName)
    {
        // Step 1: Find all movies matching the search term (case-insensitive partial match)
        var movieIds = _movies.Values
            .Where(m => m.Title.Contains(movieTitle, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Id)
            .ToHashSet();

        // Step 2: Find all screens belonging to cinemas in the specified city
        var cityScreenIds = _cinemas.Values
            .Where(c => c.City.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(c => c.Screens)
            .Select(s => s.Id)
            .ToHashSet();

        // Step 3: Return shows that match both criteria (correct movie AND correct city)
        return _shows.Values
            .Where(s => movieIds.Contains(s.Movie.Id) && cityScreenIds.Contains(s.Screen.Id))
            .ToList();
    }

    // Reverse lookup: given a show, find which cinema it's playing at.
    // Needed for display purposes (user wants to see "PVR Phoenix" not just "Screen 1").
    public Cinema? FindCinemaForShow(Show show)
    {
        return _cinemas.Values.FirstOrDefault(c => c.Screens.Any(s => s.Id == show.Screen.Id));
    }

    // Public API for booking tickets — caller only passes a PaymentType enum.
    // The PaymentProcessor inside BookingManager resolves the correct payment method.
    public Booking? BookTickets(string userId, Show show, List<Seat> seats, PaymentType paymentType)
    {
        if (!_users.TryGetValue(userId, out var user))
            throw new ArgumentException($"User '{userId}' not found");

        return _bookingManager.CreateBooking(user, show, seats, paymentType);
    }

    // ── Observer ──

    public void AddMovieObserver(IMovieObserver observer)
    {
        _movieSubject.AddObserver(observer);
    }
    public BookingManager GetBookingManager()
    {
        return _bookingManager;
    }
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var service = MovieBookingService.GetInstance();

        // ── Setup: Users ──
        var alice = service.CreateUser("u1", "Alice", "alice@mail.com");
        var bob = service.CreateUser("u2", "Bob", "bob@mail.com");

        // ── Observer: Users subscribe to movie notifications ──
        // When a new movie is added, both Alice and Bob will be notified.
        service.AddMovieObserver(new UserObserver(alice));
        service.AddMovieObserver(new UserObserver(bob));

        // ── Setup: City, Cinema, Screens, Seats ──
        var mumbai = service.AddCity("c1", "Mumbai");

        // Create a screen with mixed seat types to demonstrate tiered pricing
        var screen1 = new Screen("scr1");
        screen1.AddSeat(new Seat("s1", 1, 1, SeatType.Regular));
        screen1.AddSeat(new Seat("s2", 1, 2, SeatType.Regular));
        screen1.AddSeat(new Seat("s3", 1, 3, SeatType.Premium));
        screen1.AddSeat(new Seat("s4", 2, 1, SeatType.Premium));
        screen1.AddSeat(new Seat("s5", 2, 2, SeatType.Recliner));
        screen1.AddSeat(new Seat("s6", 2, 3, SeatType.Recliner));

        var pvr = service.AddCinema("cin1", "PVR Phoenix", mumbai, new List<Screen> { screen1 });

        // ── Add Movie (triggers observer notifications) ──
        Console.WriteLine("=== Adding Movie ===\n");
        var movie = new Movie("m1", "Interstellar", 169);
        service.AddMovie(movie);

        // ── Add Shows — one weekday, one weekend to demonstrate different pricing strategies ──
        var weekdayShow = service.AddShow("sh1", movie, screen1,
            new DateTime(2025, 7, 21, 18, 0, 0), new WeekdayPricingStrategy());

        var weekendShow = service.AddShow("sh2", movie, screen1,
            new DateTime(2025, 7, 26, 20, 0, 0), new WeekendPricingStrategy());

        // ── Search Shows ──
        Console.WriteLine("\n=== Searching: 'Interstellar' in 'Mumbai' ===\n");
        var shows = service.FindShows("Interstellar", "Mumbai");
        foreach (var s in shows)
        {
            var cinema = service.FindCinemaForShow(s);
            Console.WriteLine($"  Show: {s.Id} | {s.Movie.Title} | {cinema?.Name} | {s.StartTime:ddd dd-MMM HH:mm}");
        }

        // ── Book Tickets: Alice books 2 Regular seats (Weekday show) ──
        // Demonstrates the happy path: lock → pay → confirm
        Console.WriteLine("\n=== Alice books 2 Regular seats (Weekday, CreditCard) ===\n");
        var aliceSeats = new List<Seat> { screen1.Seats[0], screen1.Seats[1] }; // s1, s2
        var aliceBooking = service.BookTickets("u1", weekdayShow, aliceSeats, PaymentType.CreditCard);

        // ── Book Tickets: Bob tries same seats (should fail — already booked) ──
        Console.WriteLine("\n=== Bob tries same seats (should FAIL) ===\n");
        var bobBooking = service.BookTickets("u2", weekdayShow, aliceSeats, PaymentType.UPI);

        // ── Bob books different seats (Premium + Recliner, Weekend show, Wallet) ──
        Console.WriteLine("\n=== Bob books Premium + Recliner (Weekend, Wallet) ===\n");
        var bobSeats = new List<Seat> { screen1.Seats[3], screen1.Seats[4] }; // s4, s5
        bobBooking = service.BookTickets("u2", weekendShow, bobSeats, PaymentType.Wallet);

        // ── Show final seat status — confirms which seats ended up Booked ──
        Console.WriteLine("\n=== Final Seat Status (Screen 1) ===\n");
        foreach (var seat in screen1.Seats)
        {
            Console.WriteLine($"  {seat}");
        }
    }
}
