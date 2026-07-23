using System.Collections.Concurrent;
using System.Collections.Immutable;

// Meeting Scheduler V2 — Thread-Safe
//
// V1 Gaps Fixed:
//   1. RoomManager: per-room lock, CheckAndReserve is atomic (no TOCTOU)
//   2. Collections: ImmutableList for bookings, history, observers
//   3. Singleton: thread-safe with lock + double-check
//   4. HistoryService: ImmutableList for history records
//
// Same design patterns as V1: Singleton, Builder, Decorator, Strategy, Observer

// ─────────────────────────────────────────────
// Entity Classes (same as V1 — immutable)
// ─────────────────────────────────────────────
public class User
{
    public string Id { get; }
    public string Name { get; }
    public string Email { get; }
    public User(string id, string name, string email) { Id = id; Name = name; Email = email; }
    public override string ToString() => Name;
}

public class MeetingRoom
{
    public string Id { get; }
    public string Name { get; }
    public int Capacity { get; }
    public MeetingRoom(string id, string name, int capacity) { Id = id; Name = name; Capacity = capacity; }
    public override string ToString() => $"{Name} (cap:{Capacity})";
}

public class HistoryRecord
{
    public Booking Booking { get; }
    public DateTime RecordedAt { get; }
    public HistoryRecord(Booking booking) { Booking = booking; RecordedAt = DateTime.Now; }
    public override string ToString() => $"[{RecordedAt:HH:mm:ss}] {Booking}";
}

// ─────────────────────────────────────────────
// Decorator Pattern — Room Features (same as V1)
// ─────────────────────────────────────────────
public interface IRoomFeatures
{
    string GetDescription();
    int GetCost();
}

public class BasicRoom : IRoomFeatures
{
    private readonly MeetingRoom _room;
    public BasicRoom(MeetingRoom room) => _room = room;
    public string GetDescription() => $"{_room.Name} (Table & Chairs)";
    public int GetCost() => 0;
}

public abstract class RoomFeatureDecorator : IRoomFeatures
{
    protected readonly IRoomFeatures _wrapped;
    protected RoomFeatureDecorator(IRoomFeatures wrapped) => _wrapped = wrapped;
    public abstract string GetDescription();
    public abstract int GetCost();
}

public class TVFeature : RoomFeatureDecorator
{
    public TVFeature(IRoomFeatures w) : base(w) { }
    public override string GetDescription() => _wrapped.GetDescription() + " + TV";
    public override int GetCost() => _wrapped.GetCost() + 50;
}

public class WhiteboardFeature : RoomFeatureDecorator
{
    public WhiteboardFeature(IRoomFeatures w) : base(w) { }
    public override string GetDescription() => _wrapped.GetDescription() + " + Whiteboard";
    public override int GetCost() => _wrapped.GetCost() + 20;
}

public class ACFeature : RoomFeatureDecorator
{
    public ACFeature(IRoomFeatures w) : base(w) { }
    public override string GetDescription() => _wrapped.GetDescription() + " + AC";
    public override int GetCost() => _wrapped.GetCost() + 30;
}

// ─────────────────────────────────────────────
// Builder Pattern — Booking (same as V1)
// ─────────────────────────────────────────────
public class Booking
{
    public string Id { get; }
    public MeetingRoom Room { get; }
    public DateTime StartTime { get; }
    public DateTime EndTime { get; }
    public List<User> Participants { get; }
    public IRoomFeatures RoomFeatures { get; }

    public Booking(string id, MeetingRoom room, DateTime start, DateTime end,
        List<User> participants, IRoomFeatures roomFeatures)
    {
        Id = id; Room = room; StartTime = start; EndTime = end;
        Participants = participants; RoomFeatures = roomFeatures;
    }

    public override string ToString() =>
        $"Booking({Id}: {Room.Name}, {StartTime:HH:mm}-{EndTime:HH:mm}, " +
        $"{Participants.Count} people, {RoomFeatures.GetDescription()})";
}

public class BookingBuilder
{
    private readonly MeetingRoom _room;
    private readonly DateTime _start;
    private readonly DateTime _end;
    private readonly List<User> _participants;
    private bool _withTV, _withWhiteboard, _withAC;

    public BookingBuilder(MeetingRoom room, DateTime start, DateTime end, List<User> participants)
    { _room = room; _start = start; _end = end; _participants = participants; }

    public BookingBuilder WithTV() { _withTV = true; return this; }
    public BookingBuilder WithWhiteboard() { _withWhiteboard = true; return this; }
    public BookingBuilder WithAC() { _withAC = true; return this; }

    public Booking Build()
    {
        IRoomFeatures features = new BasicRoom(_room);
        if (_withTV) features = new TVFeature(features);
        if (_withWhiteboard) features = new WhiteboardFeature(features);
        if (_withAC) features = new ACFeature(features);
        return new Booking(Guid.NewGuid().ToString("N")[..8], _room, _start, _end, _participants, features);
    }
}

// ─────────────────────────────────────────────
// Strategy Pattern — Notification (same as V1)
// ─────────────────────────────────────────────
public interface INotificationStrategy
{
    void Send(User user, string message);
}

public class EmailNotification : INotificationStrategy
{
    public void Send(User user, string message) => Console.WriteLine($"    [Email → {user.Name}] {message}");
}

public class SMSNotification : INotificationStrategy
{
    public void Send(User user, string message) => Console.WriteLine($"    [SMS → {user.Name}] {message}");
}

// ─────────────────────────────────────────────
// Observer Pattern — Booking observers
// ─────────────────────────────────────────────
public interface IBookingObserver
{
    void OnBookingCreated(Booking booking);
}

// V2: NotificationService — thread-safe singleton
public class NotificationService : IBookingObserver
{
    private static NotificationService? _instance;
    private static readonly object _singletonLock = new();
    private readonly INotificationStrategy _strategy;

    private NotificationService(INotificationStrategy strategy) => _strategy = strategy;

    public static NotificationService GetInstance(INotificationStrategy? strategy = null)
    {
        if (_instance == null)
            lock (_singletonLock)
                _instance ??= new NotificationService(strategy ?? new EmailNotification());
        return _instance;
    }

    public void OnBookingCreated(Booking booking)
    {
        string msg = $"Meeting booked: {booking.Room.Name} ({booking.StartTime:HH:mm}-{booking.EndTime:HH:mm})";
        foreach (var user in booking.Participants)
            _strategy.Send(user, msg);
    }
}

// V2: HistoryService — ImmutableList for thread-safe history
public class HistoryService : IBookingObserver
{
    private static HistoryService? _instance;
    private static readonly object _singletonLock = new();
    private ImmutableList<HistoryRecord> _history = ImmutableList<HistoryRecord>.Empty;

    private HistoryService() { }
    public static HistoryService GetInstance()
    {
        if (_instance == null)
            lock (_singletonLock)
                _instance ??= new HistoryService();
        return _instance;
    }

    public void OnBookingCreated(Booking booking)
    {
        ImmutableInterlocked.Update(ref _history, list => list.Add(new HistoryRecord(booking)));
        Console.WriteLine($"    [History] Stored: {booking}");
    }

    public ImmutableList<HistoryRecord> GetHistory() => _history;
}

// ─────────────────────────────────────────────
// RoomManager — per-room lock, atomic CheckAndReserve
// ─────────────────────────────────────────────
public class RoomManager
{
    private static RoomManager? _instance;
    private static readonly object _singletonLock = new();

    private readonly ConcurrentDictionary<string, MeetingRoom> _rooms = new();
    // V2: per-room lock + schedule stored together
    private readonly ConcurrentDictionary<string, (object lockObj, List<(DateTime start, DateTime end)> schedule)> _schedules = new();

    private RoomManager() { }
    public static RoomManager GetInstance()
    {
        if (_instance == null)
            lock (_singletonLock)
                _instance ??= new RoomManager();
        return _instance;
    }

    public void AddRoom(MeetingRoom room)
    {
        _rooms.TryAdd(room.Id, room);
        _schedules.TryAdd(room.Id, (new object(), new List<(DateTime, DateTime)>()));
    }

    public MeetingRoom? GetRoom(string roomId) => _rooms.TryGetValue(roomId, out var r) ? r : null;

    // V2: Atomic check + reserve in ONE lock (no TOCTOU)
    // Returns true if reserved successfully, false if conflicting
    public bool CheckAndReserve(string roomId, DateTime start, DateTime end)
    {
        if (!_schedules.TryGetValue(roomId, out var entry)) return false;

        lock (entry.lockObj) // Per-room lock — different rooms don't block each other
        {
            // Check: does any existing booking overlap?
            bool hasConflict = entry.schedule.Any(s => start < s.end && end > s.start);
            if (hasConflict) return false;

            // Reserve: add to schedule (atomic with check)
            entry.schedule.Add((start, end));
            return true;
        }
    }

    // V2: Read-only check (for display, not for booking decisions)
    public bool CheckAvailability(string roomId, DateTime start, DateTime end)
    {
        if (!_schedules.TryGetValue(roomId, out var entry)) return false;
        lock (entry.lockObj)
        {
            return !entry.schedule.Any(s => start < s.end && end > s.start);
        }
    }

    public List<MeetingRoom> GetAvailableRooms(DateTime start, DateTime end)
    {
        return _rooms.Values.Where(r => CheckAvailability(r.Id, start, end)).ToList();
    }
}

// ─────────────────────────────────────────────
// BookingManager — ImmutableList for observers + bookings
// ─────────────────────────────────────────────
public class BookingManager
{
    private static BookingManager? _instance;
    private static readonly object _singletonLock = new();

    private ImmutableList<Booking> _bookings = ImmutableList<Booking>.Empty;
    private ImmutableList<IBookingObserver> _observers = ImmutableList<IBookingObserver>.Empty;
    private readonly RoomManager _roomManager;

    private BookingManager(RoomManager roomManager) => _roomManager = roomManager;

    public static BookingManager GetInstance(RoomManager? roomManager = null)
    {
        if (_instance == null)
            lock (_singletonLock)
                _instance ??= new BookingManager(roomManager ?? RoomManager.GetInstance());
        return _instance;
    }

    public void AddObserver(IBookingObserver observer)
    {
        ImmutableInterlocked.Update(ref _observers, list => list.Add(observer));
    }

    // V2: BookRoom uses atomic CheckAndReserve (no TOCTOU)
    public Booking? BookRoom(BookingBuilder builder, string roomId, DateTime start, DateTime end)
    {
        // Atomic check + reserve (per-room lock inside)
        if (!_roomManager.CheckAndReserve(roomId, start, end))
        {
            Console.WriteLine($"    [BookingManager] Room {roomId} NOT available for {start:HH:mm}-{end:HH:mm}");
            return null;
        }

        var booking = builder.Build();
        ImmutableInterlocked.Update(ref _bookings, list => list.Add(booking));

        Console.WriteLine($"    [BookingManager] Booked: {booking}");

        // Notify observers (snapshot iteration — safe)
        var observers = _observers;
        foreach (var obs in observers)
            obs.OnBookingCreated(booking);

        return booking;
    }

    public ImmutableList<Booking> GetAllBookings() => _bookings;
}

// ─────────────────────────────────────────────
// Demo — concurrent booking race
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var roomManager = RoomManager.GetInstance();
        var bookingManager = BookingManager.GetInstance(roomManager);
        bookingManager.AddObserver(NotificationService.GetInstance(new EmailNotification()));
        bookingManager.AddObserver(HistoryService.GetInstance());

        var room1 = new MeetingRoom("r1", "Conference A", 10);
        var room2 = new MeetingRoom("r2", "Board Room", 20);
        roomManager.AddRoom(room1);
        roomManager.AddRoom(room2);

        var alice = new User("u1", "Alice", "alice@corp.com");
        var bob = new User("u2", "Bob", "bob@corp.com");
        var charlie = new User("u3", "Charlie", "charlie@corp.com");

        var today = DateTime.Today;

        // ── Scenario 1: Concurrent booking race for same room + time ──
        Console.WriteLine("=== Scenario 1: Concurrent Booking Race (same room, same time) ===\n");

        Booking? aliceBooking = null;
        Booking? bobBooking = null;

        var aliceBuilder = new BookingBuilder(room1, today.AddHours(9), today.AddHours(10),
            new List<User> { alice, charlie }).WithTV();

        var bobBuilder = new BookingBuilder(room1, today.AddHours(9), today.AddHours(10),
            new List<User> { bob }).WithWhiteboard();

        Task.WaitAll(
            Task.Run(() => { aliceBooking = bookingManager.BookRoom(aliceBuilder, "r1", today.AddHours(9), today.AddHours(10)); }),
            Task.Run(() => { bobBooking = bookingManager.BookRoom(bobBuilder, "r1", today.AddHours(9), today.AddHours(10)); }));

        Console.WriteLine($"\n    Alice: {(aliceBooking != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"    Bob:   {(bobBooking != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"    (Exactly one should win — per-room lock prevents double-booking)\n");

        // ── Scenario 2: Different rooms in parallel (both succeed) ──
        Console.WriteLine("=== Scenario 2: Different Rooms in Parallel (both succeed) ===\n");

        Booking? booking2 = null;
        Booking? booking3 = null;

        var builder2 = new BookingBuilder(room1, today.AddHours(11), today.AddHours(12),
            new List<User> { alice }).WithAC();
        var builder3 = new BookingBuilder(room2, today.AddHours(11), today.AddHours(12),
            new List<User> { bob, charlie }).WithTV().WithWhiteboard();

        Task.WaitAll(
            Task.Run(() => { booking2 = bookingManager.BookRoom(builder2, "r1", today.AddHours(11), today.AddHours(12)); }),
            Task.Run(() => { booking3 = bookingManager.BookRoom(builder3, "r2", today.AddHours(11), today.AddHours(12)); }));

        Console.WriteLine($"\n    Room1 (Alice): {(booking2 != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"    Room2 (Bob+Charlie): {(booking3 != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"    (Both should succeed — different rooms, parallel locks)\n");

        // ── Scenario 3: Sequential booking (after first meeting) ──
        Console.WriteLine("=== Scenario 3: Sequential Booking ===\n");

        var builder4 = new BookingBuilder(room1, today.AddHours(10), today.AddHours(11),
            new List<User> { bob, charlie }).WithWhiteboard();
        bookingManager.BookRoom(builder4, "r1", today.AddHours(10), today.AddHours(11));

        // ── Available rooms ──
        Console.WriteLine("\n=== Available Rooms 9:00-10:00 ===\n");
        var available = roomManager.GetAvailableRooms(today.AddHours(9), today.AddHours(10));
        Console.WriteLine($"    Available: {string.Join(", ", available.Select(r => r.Name))}");

        // ── History ──
        Console.WriteLine("\n=== Meeting History ===\n");
        foreach (var record in HistoryService.GetInstance().GetHistory())
            Console.WriteLine($"    {record}");
    }
}
