using System.Collections.Concurrent;
using System.Collections.Immutable;

// Meeting Scheduler V3 — Calendar as Client-Facing Facade
//
// In V2, the client interacted with BookingManager and RoomManager directly.
// In V3, the client only talks to Calendar.
//
// Calendar is the SINGLE entry point:
//   calendar.GetAvailableRooms(start, end)
//   calendar.GetAvailableRooms(start, end, amenities)
//   calendar.BookRoom(roomId, start, end, participants, ...)
//   calendar.BookRoomByAmenities(start, end, participants, amenities, ...)
//   calendar.CancelBooking(bookingId)
//   calendar.ModifyBooking(bookingId, newStart, newEnd, ...)
//   calendar.GetFreeSlots(roomId, date)
//   calendar.GetBookingsForDate(roomId, date)
//
// Internally, Calendar delegates to RoomManager + BookingManager.
// The client doesn't know they exist.

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
    public HashSet<string> Amenities { get; }

    public MeetingRoom(string id, string name, int capacity, params string[] amenities)
    {
        Id = id; Name = name; Capacity = capacity;
        Amenities = new HashSet<string>(amenities, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasAmenity(string a) => Amenities.Contains(a);
    public override string ToString() => $"{Name} (cap:{Capacity}, [{string.Join(", ", Amenities)}])";
}

// ─────────────────────────────────────────────
// Decorator + Builder
// ─────────────────────────────────────────────
public interface IRoomFeatures { string GetDescription(); int GetCost(); }
public class BasicRoom : IRoomFeatures
{
    private readonly MeetingRoom _r;
    public BasicRoom(MeetingRoom r) => _r = r;
    public string GetDescription() => $"{_r.Name} (Table & Chairs)";
    public int GetCost() => 0;
}
public abstract class RoomFeatureDecorator : IRoomFeatures
{
    protected readonly IRoomFeatures _w;
    protected RoomFeatureDecorator(IRoomFeatures w) => _w = w;
    public abstract string GetDescription();
    public abstract int GetCost();
}
public class TVFeature : RoomFeatureDecorator
{
    public TVFeature(IRoomFeatures w) : base(w) { }
    public override string GetDescription() => _w.GetDescription() + " + TV";
    public override int GetCost() => _w.GetCost() + 50;
}
public class WhiteboardFeature : RoomFeatureDecorator
{
    public WhiteboardFeature(IRoomFeatures w) : base(w) { }
    public override string GetDescription() => _w.GetDescription() + " + Whiteboard";
    public override int GetCost() => _w.GetCost() + 20;
}
public class ACFeature : RoomFeatureDecorator
{
    public ACFeature(IRoomFeatures w) : base(w) { }
    public override string GetDescription() => _w.GetDescription() + " + AC";
    public override int GetCost() => _w.GetCost() + 30;
}

public class Booking
{
    public string Id { get; }
    public MeetingRoom Room { get; }
    public DateTime StartTime { get; }
    public DateTime EndTime { get; }
    public List<User> Participants { get; }
    public IRoomFeatures RoomFeatures { get; }

    public Booking(MeetingRoom room, DateTime start, DateTime end, List<User> participants, IRoomFeatures features)
    {
        Id = Guid.NewGuid().ToString("N")[..8]; Room = room; StartTime = start; EndTime = end;
        Participants = participants; RoomFeatures = features;
    }

    public override string ToString() =>
        $"Booking({Id}: {Room.Name}, {StartTime:HH:mm}-{EndTime:HH:mm}, {Participants.Count} people, {RoomFeatures.GetDescription()})";
}

// ─────────────────────────────────────────────
// Filter Strategy (dynamic room filtering)
// ─────────────────────────────────────────────
public interface IRoomFilter
{
    List<MeetingRoom> Filter(List<MeetingRoom> rooms);
}

public class AmenityFilter : IRoomFilter
{
    private readonly string _amenity;
    public AmenityFilter(string amenity) => _amenity = amenity;
    public List<MeetingRoom> Filter(List<MeetingRoom> rooms)
    {
        var result = new List<MeetingRoom>();
        for (int i = 0; i < rooms.Count; i++)
            if (rooms[i].HasAmenity(_amenity)) result.Add(rooms[i]);
        return result;
    }
}

public class MultiAmenityFilter : IRoomFilter
{
    private readonly List<string> _required;
    public MultiAmenityFilter(List<string> amenities) => _required = amenities;
    public List<MeetingRoom> Filter(List<MeetingRoom> rooms)
    {
        var result = new List<MeetingRoom>();
        for (int i = 0; i < rooms.Count; i++)
        {
            bool hasAll = true;
            for (int j = 0; j < _required.Count; j++)
                if (!rooms[i].HasAmenity(_required[j])) { hasAll = false; break; }
            if (hasAll) result.Add(rooms[i]);
        }
        return result;
    }
}

public class CapacityFilter : IRoomFilter
{
    private readonly int _min;
    public CapacityFilter(int min) => _min = min;
    public List<MeetingRoom> Filter(List<MeetingRoom> rooms)
    {
        var result = new List<MeetingRoom>();
        for (int i = 0; i < rooms.Count; i++)
            if (rooms[i].Capacity >= _min) result.Add(rooms[i]);
        return result;
    }
}

public class CompositeFilter : IRoomFilter
{
    private readonly List<IRoomFilter> _filters = new();
    public CompositeFilter Add(IRoomFilter f) { _filters.Add(f); return this; }
    public List<MeetingRoom> Filter(List<MeetingRoom> rooms)
    {
        var result = rooms;
        for (int i = 0; i < _filters.Count; i++)
            result = _filters[i].Filter(result);
        return result;
    }
}

// ─────────────────────────────────────────────
// Notification (Observer)
// ─────────────────────────────────────────────
public interface ICalendarObserver
{
    void OnBooked(Booking booking);
    void OnCancelled(Booking booking);
    void OnModified(Booking oldBooking, Booking newBooking);
}

public class NotificationObserver : ICalendarObserver
{
    public void OnBooked(Booking b)
    {
        foreach (var u in b.Participants)
            Console.WriteLine($"    [Email → {u.Name}] Meeting booked: {b.Room.Name} ({b.StartTime:HH:mm}-{b.EndTime:HH:mm})");
    }
    public void OnCancelled(Booking b) =>
        Console.WriteLine($"    [Notify] Meeting cancelled: {b.Room.Name} ({b.StartTime:HH:mm}-{b.EndTime:HH:mm})");
    public void OnModified(Booking old, Booking n) =>
        Console.WriteLine($"    [Notify] Meeting changed: {old.Room.Name} {old.StartTime:HH:mm}-{old.EndTime:HH:mm} → {n.StartTime:HH:mm}-{n.EndTime:HH:mm}");
}

public class HistoryObserver : ICalendarObserver
{
    private ImmutableList<string> _history = ImmutableList<string>.Empty;
    public void OnBooked(Booking b) { ImmutableInterlocked.Update(ref _history, l => l.Add($"BOOKED: {b}")); }
    public void OnCancelled(Booking b) { ImmutableInterlocked.Update(ref _history, l => l.Add($"CANCELLED: {b}")); }
    public void OnModified(Booking old, Booking n) { ImmutableInterlocked.Update(ref _history, l => l.Add($"MODIFIED: {old.Id} → {n}")); }
    public ImmutableList<string> GetHistory() => _history;
}

// ─────────────────────────────────────────────
// RoomSchedule — internal per-room time management (client never sees this)
// ─────────────────────────────────────────────
internal class RoomSchedule
{
    private readonly object _lock = new();
    private readonly List<(DateTime start, DateTime end)> _slots = new();

    public bool Reserve(DateTime start, DateTime end)
    {
        lock (_lock)
        {
            // Check for any overlap with existing slots
            for (int i = 0; i < _slots.Count; i++)
            {
                if (start < _slots[i].end && end > _slots[i].start)
                    return false; // conflict
            }
            _slots.Add((start, end));
            return true;
        }
    }

    public bool IsAvailable(DateTime start, DateTime end)
    {
        lock (_lock)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (start < _slots[i].end && end > _slots[i].start)
                    return false;
            }
            return true;
        }
    }

    public void Release(DateTime start, DateTime end)
    {
        lock (_lock)
        {
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                if (_slots[i].start == start && _slots[i].end == end)
                {
                    _slots.RemoveAt(i);
                    break;
                }
            }
        }
    }

    public bool ReleaseAndReserve(DateTime oldStart, DateTime oldEnd, DateTime newStart, DateTime newEnd)
    {
        lock (_lock)
        {
            // Remove old slot
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                if (_slots[i].start == oldStart && _slots[i].end == oldEnd)
                {
                    _slots.RemoveAt(i);
                    break;
                }
            }

            // Check new slot for conflicts
            for (int i = 0; i < _slots.Count; i++)
            {
                if (newStart < _slots[i].end && newEnd > _slots[i].start)
                {
                    // Rollback: re-add old slot
                    _slots.Add((oldStart, oldEnd));
                    return false;
                }
            }

            // Reserve new slot
            _slots.Add((newStart, newEnd));
            return true;
        }
    }

    public List<(DateTime start, DateTime end)> GetFreeSlots(DateTime date, int workStart = 9, int workEnd = 18)
    {
        var dayStart = date.Date.AddHours(workStart);
        var dayEnd = date.Date.AddHours(workEnd);

        lock (_lock)
        {
            // Collect today's slots and sort by start time
            var daySlots = new List<(DateTime start, DateTime end)>();
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].start < dayEnd && _slots[i].end > dayStart)
                    daySlots.Add(_slots[i]);
            }
            daySlots.Sort((a, b) => a.start.CompareTo(b.start));

            // Walk through sorted slots, collecting gaps as free time
            var free = new List<(DateTime, DateTime)>();
            var cur = dayStart;

            for (int i = 0; i < daySlots.Count; i++)
            {
                var sStart = daySlots[i].start < dayStart ? dayStart : daySlots[i].start;
                var sEnd = daySlots[i].end > dayEnd ? dayEnd : daySlots[i].end;

                if (sStart > cur)
                    free.Add((cur, sStart));
                if (sEnd > cur)
                    cur = sEnd;
            }

            if (cur < dayEnd)
                free.Add((cur, dayEnd));

            return free;
        }
    }

    public List<(DateTime start, DateTime end)> GetBookingsForDate(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        lock (_lock)
        {
            var result = new List<(DateTime start, DateTime end)>();
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].start < dayEnd && _slots[i].end > dayStart)
                    result.Add(_slots[i]);
            }
            result.Sort((a, b) => a.start.CompareTo(b.start));
            return result;
        }
    }
}

// ─────────────────────────────────────────────
// Calendar — THE client-facing facade
// ─────────────────────────────────────────────
// Client interacts ONLY with Calendar. It hides rooms, schedules, observers.
public class Calendar
{
    private readonly ConcurrentDictionary<string, MeetingRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, RoomSchedule> _schedules = new();
    private ImmutableList<Booking> _bookings = ImmutableList<Booking>.Empty;
    private ImmutableList<ICalendarObserver> _observers = ImmutableList<ICalendarObserver>.Empty;

    public void AddObserver(ICalendarObserver obs) => ImmutableInterlocked.Update(ref _observers, l => l.Add(obs));

    // ── Admin: add rooms ──
    public void AddRoom(MeetingRoom room)
    {
        _rooms.TryAdd(room.Id, room);
        _schedules.TryAdd(room.Id, new RoomSchedule());
    }

    // ── Client: get available rooms for a time slot ──
    public List<MeetingRoom> GetAvailableRooms(DateTime start, DateTime end)
    {
        var result = new List<MeetingRoom>();
        foreach (var room in _rooms.Values)
        {
            if (_schedules.TryGetValue(room.Id, out var sched) && sched.IsAvailable(start, end))
                result.Add(room);
        }
        return result;
    }

    // ── Client: get available rooms with required amenities ──
    public List<MeetingRoom> GetAvailableRooms(DateTime start, DateTime end, List<string> requiredAmenities)
    {
        var available = GetAvailableRooms(start, end);
        var filtered = new List<MeetingRoom>();
        foreach (var room in available)
        {
            bool hasAll = true;
            foreach (var amenity in requiredAmenities)
            {
                if (!room.HasAmenity(amenity)) { hasAll = false; break; }
            }
            if (hasAll) filtered.Add(room);
        }
        return filtered;
    }

    // ── Client: get available rooms with a custom filter strategy ──
    public List<MeetingRoom> GetAvailableRooms(DateTime start, DateTime end, IRoomFilter filter)
    {
        var available = GetAvailableRooms(start, end);
        return filter.Filter(available);
    }

    // ── Client: book a specific room ──
    public Booking? BookRoom(string roomId, DateTime start, DateTime end, List<User> participants,
        bool withTV = false, bool withWhiteboard = false, bool withAC = false)
    {
        if (!_rooms.TryGetValue(roomId, out var room)) return null;
        if (!_schedules.TryGetValue(roomId, out var schedule)) return null;

        if (!schedule.Reserve(start, end))
        {
            Console.WriteLine($"    [Calendar] {room.Name} NOT available {start:HH:mm}-{end:HH:mm}");
            return null;
        }

        var booking = BuildBooking(room, start, end, participants, withTV, withWhiteboard, withAC);
        ImmutableInterlocked.Update(ref _bookings, l => l.Add(booking));

        Console.WriteLine($"    [Calendar] Booked: {booking}");
        foreach (var obs in _observers) obs.OnBooked(booking);
        return booking;
    }

    // ── Client: book by amenities (calendar picks the room) ──
    public Booking? BookRoomByAmenities(DateTime start, DateTime end, List<User> participants,
        List<string> requiredAmenities, bool withTV = false, bool withWhiteboard = false, bool withAC = false)
    {
        var available = GetAvailableRooms(start, end, requiredAmenities);
        if (available.Count == 0)
        {
            Console.WriteLine($"    [Calendar] No room with [{string.Join(", ", requiredAmenities)}] available {start:HH:mm}-{end:HH:mm}");
            return null;
        }

        // Try each room (handles concurrent race — if one is grabbed, try next)
        foreach (var room in available)
        {
            if (_schedules[room.Id].Reserve(start, end))
            {
                var booking = BuildBooking(room, start, end, participants, withTV, withWhiteboard, withAC);
                ImmutableInterlocked.Update(ref _bookings, l => l.Add(booking));

                Console.WriteLine($"    [Calendar] Auto-picked {room.Name}. Booked: {booking}");
                foreach (var obs in _observers) obs.OnBooked(booking);
                return booking;
            }
        }

        Console.WriteLine($"    [Calendar] All matching rooms taken");
        return null;
    }

    // ── Client: cancel a booking ──
    public bool CancelBooking(string bookingId)
    {
        var booking = _bookings.FirstOrDefault(b => b.Id == bookingId);
        if (booking == null) return false;

        _schedules[booking.Room.Id].Release(booking.StartTime, booking.EndTime);
        ImmutableInterlocked.Update(ref _bookings, l => l.Remove(booking));

        Console.WriteLine($"    [Calendar] Cancelled: {booking}");
        foreach (var obs in _observers) obs.OnCancelled(booking);
        return true;
    }

    // ── Client: modify a booking ──
    public Booking? ModifyBooking(string bookingId, DateTime newStart, DateTime newEnd, List<User>? newParticipants = null,
        bool withTV = false, bool withWhiteboard = false, bool withAC = false)
    {
        var existing = _bookings.FirstOrDefault(b => b.Id == bookingId);
        if (existing == null) return null;

        if (!_schedules[existing.Room.Id].ReleaseAndReserve(existing.StartTime, existing.EndTime, newStart, newEnd))
        {
            Console.WriteLine($"    [Calendar] Cannot modify — new time not available. Original preserved.");
            return null;
        }

        var newBooking = BuildBooking(existing.Room, newStart, newEnd,
            newParticipants ?? existing.Participants, withTV, withWhiteboard, withAC);
        ImmutableInterlocked.Update(ref _bookings, l => l.Remove(existing).Add(newBooking));

        Console.WriteLine($"    [Calendar] Modified: {existing.Id} → {newBooking}");
        foreach (var obs in _observers) obs.OnModified(existing, newBooking);
        return newBooking;
    }

    // ── Client: get free slots for a room on a date ──
    public List<(DateTime start, DateTime end)> GetFreeSlots(string roomId, DateTime date, int workStart = 9, int workEnd = 18)
    {
        if (!_schedules.TryGetValue(roomId, out var sched)) return new();
        return sched.GetFreeSlots(date, workStart, workEnd);
    }

    // ── Client: get bookings for a room on a date ──
    public List<(DateTime start, DateTime end)> GetBookingsForDate(string roomId, DateTime date)
    {
        if (!_schedules.TryGetValue(roomId, out var sched)) return new();
        return sched.GetBookingsForDate(date);
    }

    // ── Client: get all rooms ──
    public List<MeetingRoom> GetAllRooms()
    {
        var result = new List<MeetingRoom>();
        foreach (var room in _rooms.Values)
            result.Add(room);
        return result;
    }

    // ── Internal builder helper ──
    private Booking BuildBooking(MeetingRoom room, DateTime start, DateTime end, List<User> participants,
        bool withTV, bool withWhiteboard, bool withAC)
    {
        IRoomFeatures features = new BasicRoom(room);
        if (withTV) features = new TVFeature(features);
        if (withWhiteboard) features = new WhiteboardFeature(features);
        if (withAC) features = new ACFeature(features);
        return new Booking(room, start, end, participants, features);
    }
}

// ─────────────────────────────────────────────
// Demo — Client interacts ONLY with Calendar
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        // The Calendar is the ONLY thing the client uses
        var calendar = new Calendar();
        calendar.AddObserver(new NotificationObserver());
        var history = new HistoryObserver();
        calendar.AddObserver(history);

        // Admin: add rooms (one-time setup)
        calendar.AddRoom(new MeetingRoom("r1", "Conference A", 10, "TV", "Whiteboard", "AC"));
        calendar.AddRoom(new MeetingRoom("r2", "Board Room", 20, "TV", "Projector", "VideoConf", "AC"));
        calendar.AddRoom(new MeetingRoom("r3", "Huddle Space", 4, "Whiteboard"));

        var alice = new User("u1", "Alice", "alice@corp.com");
        var bob = new User("u2", "Bob", "bob@corp.com");
        var charlie = new User("u3", "Charlie", "charlie@corp.com");
        var today = DateTime.Today;

        // ── Client asks Calendar: what rooms are available 9-10? ──
        Console.WriteLine("=== Client: Available Rooms 9:00-10:00 ===\n");
        var available = calendar.GetAvailableRooms(today.AddHours(9), today.AddHours(10));
        foreach (var r in available)
            Console.WriteLine($"    {r}");

        // ── Client asks Calendar: book Conference A 9-10 ──
        Console.WriteLine("\n=== Client: Book Conference A 9:00-10:00 ===\n");
        var b1 = calendar.BookRoom("r1", today.AddHours(9), today.AddHours(10),
            new List<User> { alice, bob }, withTV: true);

        // ── Client asks Calendar: available rooms with Projector + VideoConf? ──
        Console.WriteLine("\n=== Client: Available Rooms with Projector + VideoConf ===\n");
        var projRooms = calendar.GetAvailableRooms(today.AddHours(9), today.AddHours(10),
            new List<string> { "Projector", "VideoConf" });
        foreach (var r in projRooms)
            Console.WriteLine($"    {r}");

        // ── Client asks Calendar: book by amenities (auto-pick) ──
        Console.WriteLine("\n=== Client: Book by Amenities (Projector + VideoConf) ===\n");
        var b2 = calendar.BookRoomByAmenities(today.AddHours(9), today.AddHours(10),
            new List<User> { alice, bob, charlie },
            new List<string> { "Projector", "VideoConf" }, withTV: true);

        // ── Double-book fails ──
        Console.WriteLine("\n=== Client: Double-book Conference A 9-10 (fails) ===\n");
        calendar.BookRoom("r1", today.AddHours(9), today.AddHours(10), new List<User> { charlie });

        // ── Client asks Calendar: free slots for Conference A today ──
        Console.WriteLine("\n=== Client: Free Slots — Conference A ===\n");
        var freeSlots = calendar.GetFreeSlots("r1", today);
        foreach (var (start, end) in freeSlots)
            Console.WriteLine($"    Free: {start:HH:mm} — {end:HH:mm}");

        // ── Client books another slot ──
        Console.WriteLine("\n=== Client: Book Conference A 14:00-16:00 ===\n");
        var b3 = calendar.BookRoom("r1", today.AddHours(14), today.AddHours(16),
            new List<User> { bob }, withWhiteboard: true);

        // ── Client checks free slots again ──
        Console.WriteLine("\n=== Client: Updated Free Slots — Conference A ===\n");
        foreach (var (start, end) in calendar.GetFreeSlots("r1", today))
            Console.WriteLine($"    Free: {start:HH:mm} — {end:HH:mm}");

        // ── Client cancels a booking via Calendar ──
        Console.WriteLine("\n=== Client: Cancel 9:00-10:00 booking ===\n");
        calendar.CancelBooking(b1!.Id);
        Console.WriteLine($"    Conference A 9-10 now available: {calendar.GetAvailableRooms(today.AddHours(9), today.AddHours(10)).Any(r => r.Id == "r1")}");

        // ── Client modifies via Calendar ──
        Console.WriteLine("\n=== Client: Modify 14:00-16:00 → 11:00-12:00 ===\n");
        calendar.ModifyBooking(b3!.Id, today.AddHours(11), today.AddHours(12), withAC: true);

        // ── Final state ──
        Console.WriteLine("\n=== Final: Conference A Free Slots ===\n");
        foreach (var (start, end) in calendar.GetFreeSlots("r1", today))
            Console.WriteLine($"    Free: {start:HH:mm} — {end:HH:mm}");

        // ── History ──
        Console.WriteLine("\n=== History ===\n");
        foreach (var entry in history.GetHistory())
            Console.WriteLine($"    {entry}");
    }
}
