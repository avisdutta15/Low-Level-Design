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
    public HashSet<string> Amenities { get; } // e.g., "TV", "Whiteboard", "AC", "Projector", "VideoConf"

    public MeetingRoom(string id, string name, int capacity, params string[] amenities)
    {
        Id = id; Name = name; Capacity = capacity;
        Amenities = new HashSet<string>(amenities, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasAmenity(string amenity) => Amenities.Contains(amenity);
    public override string ToString() => $"{Name} (cap:{Capacity}, [{string.Join(", ", Amenities)}])";
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
// Room Filter Strategy — dynamic filtering
// ─────────────────────────────────────────────

// IRoomFilter: each filter takes a list of rooms and returns matching ones.
// Filters can be combined (AND logic) by chaining them.
public interface IRoomFilter
{
    List<MeetingRoom> Filter(List<MeetingRoom> rooms);
}

// Filter by minimum capacity
public class CapacityFilter : IRoomFilter
{
    private readonly int _minCapacity;
    public CapacityFilter(int minCapacity) => _minCapacity = minCapacity;
    public List<MeetingRoom> Filter(List<MeetingRoom> rooms) =>
        rooms.Where(r => r.Capacity >= _minCapacity).ToList();
}

// Filter by a required amenity (e.g., "TV", "Whiteboard")
public class AmenityFilter : IRoomFilter
{
    private readonly string _amenity;
    public AmenityFilter(string amenity) => _amenity = amenity;
    public List<MeetingRoom> Filter(List<MeetingRoom> rooms) =>
        rooms.Where(r => r.HasAmenity(_amenity)).ToList();
}

// Filter by multiple required amenities (room must have ALL of them)
public class MultiAmenityFilter : IRoomFilter
{
    private readonly List<string> _requiredAmenities;
    public MultiAmenityFilter(List<string> amenities) => _requiredAmenities = amenities;
    public List<MeetingRoom> Filter(List<MeetingRoom> rooms) =>
        rooms.Where(r => _requiredAmenities.All(a => r.HasAmenity(a))).ToList();
}

// Composite filter: chains multiple filters (AND logic)
// Apply filter1, then filter2 on the result, etc.
public class CompositeFilter : IRoomFilter
{
    private readonly List<IRoomFilter> _filters = new();

    public CompositeFilter Add(IRoomFilter filter) { _filters.Add(filter); return this; }

    public List<MeetingRoom> Filter(List<MeetingRoom> rooms)
    {
        var result = rooms;
        foreach (var filter in _filters)
            result = filter.Filter(result);
        return result;
    }
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

    // Filter rooms by strategy: availability + any additional filter (amenity, capacity, etc.)
    public List<MeetingRoom> FilterRooms(DateTime start, DateTime end, IRoomFilter? filter = null)
    {
        // Start with rooms available in the time slot
        var available = GetAvailableRooms(start, end);
        // Apply additional filter if provided
        if (filter != null)
            available = filter.Filter(available);
        return available;
    }

    // Release a reserved time slot (used by cancel/delete)
    public void ReleaseSlot(string roomId, DateTime start, DateTime end)
    {
        if (_schedules.TryGetValue(roomId, out var schedule))
            schedule.RemoveAll(s => s.start == start && s.end == end);
    }
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

    // Book by amenities: filter available rooms with required amenities, auto-pick first match
    // Caller doesn't need to know which room — system picks the best available one.
    public Booking? BookRoomByAmenities(DateTime start, DateTime end, List<User> participants,
        List<string> requiredAmenities, bool withTV = false, bool withWhiteboard = false, bool withAC = false)
    {
        // Filter: available rooms that have ALL required amenities
        var filter = new MultiAmenityFilter(requiredAmenities);
        var matchingRooms = _roomManager.FilterRooms(start, end, filter);

        if (matchingRooms.Count == 0)
        {
            Console.WriteLine($"    [BookingManager] No room with [{string.Join(", ", requiredAmenities)}] available for {start:HH:mm}-{end:HH:mm}");
            return null;
        }

        // Pick the first matching room (could use a strategy here: smallest, largest, etc.)
        var room = matchingRooms.First();
        Console.WriteLine($"    [BookingManager] Auto-selected: {room}");

        // Build booking with selected room + optional decorator features
        var builder = new BookingBuilder(room, start, end, participants);
        if (withTV) builder.WithTV();
        if (withWhiteboard) builder.WithWhiteboard();
        if (withAC) builder.WithAC();

        return BookRoom(builder, room.Id, start, end);
    }

    public List<Booking> GetAllBookings() => _bookings.ToList();

    // Cancel a booking: release the room slot, remove from bookings, notify observers
    public bool CancelBooking(string bookingId)
    {
        var booking = _bookings.FirstOrDefault(b => b.Id == bookingId);
        if (booking == null)
        {
            Console.WriteLine($"    [BookingManager] Booking {bookingId} not found");
            return false;
        }

        // Release the time slot back to the room
        _roomManager.ReleaseSlot(booking.Room.Id, booking.StartTime, booking.EndTime);
        _bookings.Remove(booking);

        Console.WriteLine($"    [BookingManager] Cancelled: {booking}");

        // Notify observers about cancellation
        foreach (var obs in _observers)
            obs.OnBookingCreated(booking); // reuse observer (in production: separate OnBookingCancelled)

        return true;
    }

    // Modify a booking: cancel old, rebook with new time/features
    // Returns the new booking if successful, null if new slot unavailable (old booking preserved)
    public Booking? ModifyBooking(string bookingId, BookingBuilder newBuilder, DateTime newStart, DateTime newEnd)
    {
        var existing = _bookings.FirstOrDefault(b => b.Id == bookingId);
        if (existing == null)
        {
            Console.WriteLine($"    [BookingManager] Booking {bookingId} not found");
            return null;
        }

        string roomId = existing.Room.Id;

        // Release old slot first
        _roomManager.ReleaseSlot(roomId, existing.StartTime, existing.EndTime);

        // Try to reserve new slot
        if (!_roomManager.CheckAvailability(roomId, newStart, newEnd))
        {
            // Rollback: re-reserve the old slot
            _roomManager.ReserveSlot(roomId, existing.StartTime, existing.EndTime);
            Console.WriteLine($"    [BookingManager] Cannot modify — new time not available. Original preserved.");
            return null;
        }

        // Reserve new slot
        _roomManager.ReserveSlot(roomId, newStart, newEnd);

        // Replace booking
        _bookings.Remove(existing);
        var newBooking = newBuilder.Build();
        _bookings.Add(newBooking);

        Console.WriteLine($"    [BookingManager] Modified: {existing.Id} → {newBooking}");

        foreach (var obs in _observers)
            obs.OnBookingCreated(newBooking);

        return newBooking;
    }
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

        // Add rooms (with amenities)
        var room1 = new MeetingRoom("r1", "Conference A", 10, "TV", "Whiteboard", "AC");
        var room2 = new MeetingRoom("r2", "Board Room", 20, "TV", "Projector", "VideoConf", "AC");
        var room3 = new MeetingRoom("r3", "Huddle Space", 4, "Whiteboard");
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

        // ── Scenario 7: Cancel a booking ──
        Console.WriteLine("\n=== Scenario 7: Cancel Huddle Space booking ===\n");
        var allBookings = bookingManager.GetAllBookings();
        var huddleBooking = allBookings.FirstOrDefault(b => b.Room.Name == "Huddle Space");
        if (huddleBooking != null)
        {
            bookingManager.CancelBooking(huddleBooking.Id);
            // Verify room is now free
            var huddleAvailable = roomManager.CheckAvailability("r3", today.AddHours(14), today.AddHours(15));
            Console.WriteLine($"    Huddle Space 14:00-15:00 available after cancel: {huddleAvailable}");
        }

        // ── Scenario 8: Modify a booking (change time) ──
        Console.WriteLine("\n=== Scenario 8: Modify Conference A 10:00-11:00 → 15:00-16:00 ===\n");
        var confBooking = allBookings.FirstOrDefault(b => b.Room.Name == "Conference A" && b.StartTime.Hour == 10);
        if (confBooking != null)
        {
            var modBuilder = new BookingBuilder(room1, today.AddHours(15), today.AddHours(16),
                new List<User> { alice, bob }).WithTV().WithAC();

            bookingManager.ModifyBooking(confBooking.Id, modBuilder, today.AddHours(15), today.AddHours(16));

            // Old slot should be free now
            var oldFree = roomManager.CheckAvailability("r1", today.AddHours(10), today.AddHours(11));
            Console.WriteLine($"    Conference A 10:00-11:00 free after modify: {oldFree}");
        }

        // ── Scenario 9: Modify fails (new time conflicts) ──
        Console.WriteLine("\n=== Scenario 9: Modify fails (conflict with existing) ===\n");
        var firstBooking = allBookings.FirstOrDefault(b => b.Room.Name == "Conference A" && b.StartTime.Hour == 9);
        if (firstBooking != null)
        {
            // Try to move to 15:00-16:00 which was just taken by Scenario 8
            var failBuilder = new BookingBuilder(room1, today.AddHours(15), today.AddHours(16),
                new List<User> { charlie });

            bookingManager.ModifyBooking(firstBooking.Id, failBuilder, today.AddHours(15), today.AddHours(16));
        }

        // ── Scenario 10: Filter rooms by amenity ──
        Console.WriteLine("\n=== Scenario 10: Filter Rooms ===\n");

        // All rooms (for reference)
        Console.WriteLine("    All rooms:");
        foreach (var r in roomManager.GetAllRooms())
            Console.WriteLine($"      {r}");

        // Filter: rooms with TV, available 14:00-15:00
        Console.WriteLine("\n    Filter: has TV, available 14:00-15:00:");
        var tvRooms = roomManager.FilterRooms(today.AddHours(14), today.AddHours(15), new AmenityFilter("TV"));
        foreach (var r in tvRooms)
            Console.WriteLine($"      {r}");

        // Filter: capacity >= 10 AND has AC
        Console.WriteLine("\n    Filter: capacity >= 10 AND has AC:");
        var compositeFilter = new CompositeFilter()
            .Add(new CapacityFilter(10))
            .Add(new AmenityFilter("AC"));
        var bigAcRooms = roomManager.FilterRooms(today.AddHours(14), today.AddHours(15), compositeFilter);
        foreach (var r in bigAcRooms)
            Console.WriteLine($"      {r}");

        // Filter: must have BOTH Projector AND VideoConf
        Console.WriteLine("\n    Filter: has Projector AND VideoConf:");
        var multiFilter = new MultiAmenityFilter(new List<string> { "Projector", "VideoConf" });
        var projRooms = roomManager.FilterRooms(today.AddHours(14), today.AddHours(15), multiFilter);
        foreach (var r in projRooms)
            Console.WriteLine($"      {r}");

        // Filter: Whiteboard only (Huddle Space was cancelled — should be available again)
        Console.WriteLine("\n    Filter: has Whiteboard:");
        var wbRooms = roomManager.FilterRooms(today.AddHours(14), today.AddHours(15), new AmenityFilter("Whiteboard"));
        foreach (var r in wbRooms)
            Console.WriteLine($"      {r}");

        // ── Scenario 11: Book by amenities (auto-pick room) ──
        Console.WriteLine("\n=== Scenario 11: Book by Amenities (auto-pick room) ===\n");

        // Need a room with Projector + VideoConf at 16:00-17:00
        Console.WriteLine("    Need: Projector + VideoConf, 16:00-17:00");
        var autoBooking = bookingManager.BookRoomByAmenities(
            today.AddHours(16), today.AddHours(17),
            new List<User> { alice, bob, charlie },
            new List<string> { "Projector", "VideoConf" },
            withTV: true);

        // Need a room with Whiteboard at 16:00-17:00 (Huddle Space or Conference A match)
        Console.WriteLine("\n    Need: Whiteboard, 16:00-17:00");
        var autoBooking2 = bookingManager.BookRoomByAmenities(
            today.AddHours(16), today.AddHours(17),
            new List<User> { alice },
            new List<string> { "Whiteboard" },
            withWhiteboard: true);

        // Need: VideoConf at 16:00-17:00 (Board Room already booked above — should fail)
        Console.WriteLine("\n    Need: Projector + VideoConf, 16:00-17:00 again (should fail — already booked)");
        var autoBooking3 = bookingManager.BookRoomByAmenities(
            today.AddHours(16), today.AddHours(17),
            new List<User> { bob },
            new List<string> { "Projector", "VideoConf" });
    }
}
