// Meeting Scheduler V1
//
// Design Patterns:
//   - Singleton: RoomManager, BookingManager, NotificationService
//   - Builder: BookingBuilder (optional features: TV, whiteboard, AC)
//   - Decorator: RoomFeatures (dynamically enhance room with selected features)
//   - Strategy: NotificationStrategy (Email, SMS, Push)
//   - Observer: BookingManager notifies NotificationService + HistoryService after booking

// ─────────────────────────────────────────────
// Entity Classes
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

public class Notification
{
    public string Content { get; }
    public DateTime SentAt { get; }
    public Notification(string content) { Content = content; SentAt = DateTime.Now; }
}

public class HistoryRecord
{
    public Booking Booking { get; }
    public DateTime RecordedAt { get; }
    public HistoryRecord(Booking booking) { Booking = booking; RecordedAt = DateTime.Now; }
    public override string ToString() => $"[{RecordedAt:HH:mm:ss}] {Booking}";
}

// ─────────────────────────────────────────────
// Decorator Pattern — Room Features
// ─────────────────────────────────────────────

// Base interface: every room feature has a description and optional cost
public interface IRoomFeatures
{
    string GetDescription();
    int GetCost();
}

// BasicRoom: table & chairs (default for every room)
public class BasicRoom : IRoomFeatures
{
    private readonly MeetingRoom _room;
    public BasicRoom(MeetingRoom room) => _room = room;
    public string GetDescription() => $"{_room.Name} (Table & Chairs)";
    public int GetCost() => 0;
}

// Decorator base
public abstract class RoomFeatureDecorator : IRoomFeatures
{
    protected readonly IRoomFeatures _wrapped;
    protected RoomFeatureDecorator(IRoomFeatures wrapped) => _wrapped = wrapped;
    public abstract string GetDescription();
    public abstract int GetCost();
}

// Concrete decorators
public class TVFeature : RoomFeatureDecorator
{
    public TVFeature(IRoomFeatures wrapped) : base(wrapped) { }
    public override string GetDescription() => _wrapped.GetDescription() + " + TV";
    public override int GetCost() => _wrapped.GetCost() + 50;
}

public class WhiteboardFeature : RoomFeatureDecorator
{
    public WhiteboardFeature(IRoomFeatures wrapped) : base(wrapped) { }
    public override string GetDescription() => _wrapped.GetDescription() + " + Whiteboard";
    public override int GetCost() => _wrapped.GetCost() + 20;
}

public class ACFeature : RoomFeatureDecorator
{
    public ACFeature(IRoomFeatures wrapped) : base(wrapped) { }
    public override string GetDescription() => _wrapped.GetDescription() + " + AC";
    public override int GetCost() => _wrapped.GetCost() + 30;
}

// ─────────────────────────────────────────────
// Builder Pattern — Booking with optional features
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
    private bool _withTV;
    private bool _withWhiteboard;
    private bool _withAC;

    public BookingBuilder(MeetingRoom room, DateTime start, DateTime end, List<User> participants)
    {
        _room = room; _start = start; _end = end; _participants = participants;
    }

    public BookingBuilder WithTV() { _withTV = true; return this; }
    public BookingBuilder WithWhiteboard() { _withWhiteboard = true; return this; }
    public BookingBuilder WithAC() { _withAC = true; return this; }

    public Booking Build()
    {
        // Start with BasicRoom, then wrap with decorators based on selections
        IRoomFeatures features = new BasicRoom(_room);
        if (_withTV) features = new TVFeature(features);
        if (_withWhiteboard) features = new WhiteboardFeature(features);
        if (_withAC) features = new ACFeature(features);

        return new Booking(Guid.NewGuid().ToString("N")[..8], _room, _start, _end, _participants, features);
    }
}

// ─────────────────────────────────────────────
// Strategy Pattern — Notification Types
// ─────────────────────────────────────────────
public interface INotificationStrategy
{
    void Send(User user, string message);
}

public class EmailNotification : INotificationStrategy
{
    public void Send(User user, string message)
    {
        Console.WriteLine($"    [Email → {user.Name}] {message}");
    }
}

public class SMSNotification : INotificationStrategy
{
    public void Send(User user, string message)
    {
        Console.WriteLine($"    [SMS → {user.Name}] {message}");
    }
}

public class PushNotification : INotificationStrategy
{
    public void Send(User user, string message)
    {
        Console.WriteLine($"    [Push → {user.Name}] {message}");
    }
}

// Factory for notification strategy
public static class NotificationFactory
{
    public static INotificationStrategy Create(string type)
    {
        if (type == "email") return new EmailNotification();
        else if (type == "sms") return new SMSNotification();
        else if (type == "push") return new PushNotification();
        else return new EmailNotification(); // default
    }
}

// ─────────────────────────────────────────────
// Observer Pattern — Booking observers
// ─────────────────────────────────────────────
public interface IBookingObserver
{
    void OnBookingCreated(Booking booking);
}

// NotificationService: sends notifications to all participants
public class NotificationService : IBookingObserver
{
    private static NotificationService? _instance;
    private readonly INotificationStrategy _strategy;

    private NotificationService(INotificationStrategy strategy) => _strategy = strategy;

    public static NotificationService GetInstance(INotificationStrategy? strategy = null)
    {
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

// HistoryService: stores meeting history
public class HistoryService : IBookingObserver
{
    private static HistoryService? _instance;
    private readonly List<HistoryRecord> _history = new();

    private HistoryService() { }
    public static HistoryService GetInstance()
    {
        _instance ??= new HistoryService();
        return _instance;
    }

    public void OnBookingCreated(Booking booking)
    {
        _history.Add(new HistoryRecord(booking));
        Console.WriteLine($"    [History] Stored: {booking}");
    }

    public List<HistoryRecord> GetHistory() => _history.ToList();
}

// ─────────────────────────────────────────────
// Singleton — RoomManager (manages rooms + availability)
// ─────────────────────────────────────────────
public class RoomManager
{
    private static RoomManager? _instance;
    private readonly Dictionary<string, MeetingRoom> _rooms = new();
    private readonly Dictionary<string, List<(DateTime start, DateTime end)>> _schedules = new();

    private RoomManager() { }
    public static RoomManager GetInstance()
    {
        _instance ??= new RoomManager();
        return _instance;
    }

    public void AddRoom(MeetingRoom room)
    {
        _rooms[room.Id] = room;
        _schedules[room.Id] = new List<(DateTime, DateTime)>();
    }

    public MeetingRoom? GetRoom(string roomId) => _rooms.TryGetValue(roomId, out var r) ? r : null;

    // Check if a room is free in the given time slot
    public bool CheckAvailability(string roomId, DateTime start, DateTime end)
    {
        if (!_schedules.TryGetValue(roomId, out var schedule)) return false;
        // Room is available if no existing booking overlaps
        return !schedule.Any(s => start < s.end && end > s.start);
    }

    // Book the time slot (mark as occupied)
    public void ReserveSlot(string roomId, DateTime start, DateTime end)
    {
        if (_schedules.TryGetValue(roomId, out var schedule))
            schedule.Add((start, end));
    }

    // Get all available rooms for a time slot
    public List<MeetingRoom> GetAvailableRooms(DateTime start, DateTime end)
    {
        return _rooms.Values
            .Where(r => CheckAvailability(r.Id, start, end))
            .ToList();
    }

    public List<MeetingRoom> GetAllRooms() => _rooms.Values.ToList();
}

// ─────────────────────────────────────────────
// Singleton — BookingManager (orchestrates booking + notifies observers)
// ─────────────────────────────────────────────
public class BookingManager
{
    private static BookingManager? _instance;
    private readonly List<Booking> _bookings = new();
    private readonly List<IBookingObserver> _observers = new();
    private readonly RoomManager _roomManager;

    private BookingManager(RoomManager roomManager) => _roomManager = roomManager;

    public static BookingManager GetInstance(RoomManager? roomManager = null)
    {
        _instance ??= new BookingManager(roomManager ?? RoomManager.GetInstance());
        return _instance;
    }

    public void AddObserver(IBookingObserver observer) => _observers.Add(observer);

    // Main booking function: validates availability, creates booking, notifies observers
    public Booking? BookRoom(BookingBuilder builder, string roomId, DateTime start, DateTime end)
    {
        // Check availability
        if (!_roomManager.CheckAvailability(roomId, start, end))
        {
            Console.WriteLine($"    [BookingManager] Room {roomId} is NOT available for {start:HH:mm}-{end:HH:mm}");
            return null;
        }

        // Build the booking (Builder applies decorators)
        var booking = builder.Build();

        // Reserve the slot
        _roomManager.ReserveSlot(roomId, start, end);
        _bookings.Add(booking);

        Console.WriteLine($"    [BookingManager] Booked: {booking}");

        // Notify observers (NotificationService + HistoryService)
        foreach (var obs in _observers)
            obs.OnBookingCreated(booking);

        return booking;
    }

    public List<Booking> GetAllBookings() => _bookings.ToList();
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        // Setup singletons
        var roomManager = RoomManager.GetInstance();
        var bookingManager = BookingManager.GetInstance(roomManager);
        var notificationService = NotificationService.GetInstance(new EmailNotification());
        var historyService = HistoryService.GetInstance();

        // Register observers
        bookingManager.AddObserver(notificationService);
        bookingManager.AddObserver(historyService);

        // Add rooms
        var room1 = new MeetingRoom("r1", "Conference A", 10);
        var room2 = new MeetingRoom("r2", "Board Room", 20);
        var room3 = new MeetingRoom("r3", "Huddle Space", 4);
        roomManager.AddRoom(room1);
        roomManager.AddRoom(room2);
        roomManager.AddRoom(room3);

        // Create users
        var alice = new User("u1", "Alice", "alice@corp.com");
        var bob = new User("u2", "Bob", "bob@corp.com");
        var charlie = new User("u3", "Charlie", "charlie@corp.com");

        var today = DateTime.Today;

        // ── Scenario 1: Book with optional features (Builder + Decorator) ──
        Console.WriteLine("=== Scenario 1: Book Conference A with TV + Whiteboard ===\n");

        var builder1 = new BookingBuilder(room1, today.AddHours(9), today.AddHours(10),
            new List<User> { alice, bob, charlie })
            .WithTV()
            .WithWhiteboard();

        bookingManager.BookRoom(builder1, "r1", today.AddHours(9), today.AddHours(10));

        // ── Scenario 2: Double booking attempt (should fail) ──
        Console.WriteLine("\n=== Scenario 2: Double Booking (same room, overlapping time) ===\n");

        var builder2 = new BookingBuilder(room1, today.AddHours(9), today.AddHours(11),
            new List<User> { bob })
            .WithAC();

        bookingManager.BookRoom(builder2, "r1", today.AddHours(9), today.AddHours(11));

        // ── Scenario 3: Book different room same time (should succeed) ──
        Console.WriteLine("\n=== Scenario 3: Book Board Room (same time, different room) ===\n");

        var builder3 = new BookingBuilder(room2, today.AddHours(9), today.AddHours(10),
            new List<User> { bob, charlie })
            .WithAC()
            .WithTV();

        bookingManager.BookRoom(builder3, "r2", today.AddHours(9), today.AddHours(10));

        // ── Scenario 4: Check available rooms ──
        Console.WriteLine("\n=== Scenario 4: Available rooms 9:00-10:00 ===\n");

        var available = roomManager.GetAvailableRooms(today.AddHours(9), today.AddHours(10));
        Console.WriteLine($"    Available: {string.Join(", ", available.Select(r => r.Name))}");

        // ── Scenario 5: Book after first meeting ends ──
        Console.WriteLine("\n=== Scenario 5: Book Conference A after first meeting (10:00-11:00) ===\n");

        var builder4 = new BookingBuilder(room1, today.AddHours(10), today.AddHours(11),
            new List<User> { alice })
            .WithWhiteboard();

        bookingManager.BookRoom(builder4, "r1", today.AddHours(10), today.AddHours(11));

        // ── Scenario 6: Simple booking (no optional features) ──
        Console.WriteLine("\n=== Scenario 6: Huddle Space, no extras ===\n");

        var builder5 = new BookingBuilder(room3, today.AddHours(14), today.AddHours(15),
            new List<User> { alice, bob });

        bookingManager.BookRoom(builder5, "r3", today.AddHours(14), today.AddHours(15));

        // ── History ──
        Console.WriteLine("\n=== Meeting History ===\n");
        foreach (var record in historyService.GetHistory())
            Console.WriteLine($"    {record}");
    }
}
