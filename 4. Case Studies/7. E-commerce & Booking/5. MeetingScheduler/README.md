# Meeting Scheduler System

## Table of Contents

- [Problem Statement](#problem-statement)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Core Entities](#core-entities)
- [Design Patterns](#design-patterns)
- [V1 — Basic Pipeline](#v1--basic-pipeline)
- [V1 to V2](#v1-to-v2)
- [V2 — Fully Thread-Safe](#v2--fully-thread-safe)

---

## Problem Statement

A meeting scheduler allows users to book meeting rooms for specific time slots. Rooms have optional features (TV, whiteboard, AC). The system prevents double-bookings, notifies participants, and maintains meeting history.

---

## Functional Requirements

- Book a meeting room from start time to end time if available
- Show room unavailability immediately if conflicting
- Notify all participants of a booking
- Store history of all scheduled meetings
- View all available rooms for a time slot
- Support optional room features (TV, whiteboard, AC) at booking time

---

## Non-Functional Requirements

- **Thread-safety**: Handle concurrent booking requests without double-booking
- **Atomicity**: Check availability + reserve must be atomic
- **Extensibility**: Easy to add new features, notification channels, room types
- **Maintainability**: OO principles, testable in isolation

---

## Core Entities

| Entity | Responsibility |
|--------|---------------|
| **User** | Organizer/participant |
| **MeetingRoom** | Bookable room (id, name, capacity) |
| **Booking** | Scheduled meeting: room + time + participants + features |
| **HistoryRecord** | Stored record of a past booking |
| **IRoomFeatures** | Decorator interface: description + cost |
| **BasicRoom / TVFeature / WhiteboardFeature / ACFeature** | Decorator chain |
| **BookingBuilder** | Builder: fluent API for optional features |
| **INotificationStrategy** | Strategy: Email / SMS / Push |
| **IBookingObserver** | Observer: NotificationService, HistoryService |
| **RoomManager** | Singleton: manages rooms + schedules |
| **BookingManager** | Singleton: orchestrates booking + notifies observers |

### Why RoomManager Exists (Separate from BookingManager)

`RoomManager` is the **single source of truth** for which rooms exist and when they're occupied. Separating it from `BookingManager` provides:

**Single Responsibility:**
- `RoomManager` = "what rooms exist and when are they free" (resource management)
- `BookingManager` = "orchestrate a booking: check → build → reserve → notify" (workflow)

**Reusability — other services need room state without booking:**
- A "find me a room" UI calls `GetAvailableRooms()` — doesn't create a booking
- A dashboard shows room occupancy — reads schedules, never writes
- A maintenance system marks rooms unavailable — modifies room state without bookings

**Lock ownership is clear (V2):**
The per-room lock lives inside `RoomManager` because it protects the *schedule* — a concern of room management, not booking orchestration.

```
Without RoomManager (simple):
  BookingManager does everything → one class, tightly coupled

With RoomManager (extensible):
  BookingManager → asks RoomManager "is it free? reserve it"
  Dashboard → asks RoomManager "show me all room schedules"
  Maintenance → tells RoomManager "mark room unavailable"
  All use the same source of truth, without touching booking logic.
```

If the system is truly simple (no dashboard, no maintenance, no multi-service access), you can fold `RoomManager` into `BookingManager`. The separation is a design choice for extensibility.

---

## Design Patterns

| Pattern | Usage |
|---------|-------|
| **Singleton** | RoomManager, BookingManager, NotificationService, HistoryService |
| **Builder** | BookingBuilder — `.WithTV().WithWhiteboard().WithAC().Build()` |
| **Decorator** | BasicRoom → TVFeature → WhiteboardFeature → ACFeature (stacked) |
| **Strategy** | INotificationStrategy: Email, SMS, Push + NotificationFactory |
| **Observer** | BookingManager notifies NotificationService + HistoryService after booking |

---

## V1 — Basic Pipeline

### V1 Class Diagram 
![alt text](v1-cd.png)

### V1 Booking Flow

```
user calls: bookingManager.BookRoom(builder, "r1", 9:00, 10:00)
│
├─ Step 1: roomManager.CheckAvailability("r1", 9:00, 10:00)
│     → scans schedule list for overlaps
│     → returns true (no conflict)
│
├─ Step 2: builder.Build()
│     → BasicRoom("Conference A")
│     → wrap with TVFeature
│     → wrap with WhiteboardFeature
│     → returns Booking with decorated features
│
├─ Step 3: roomManager.ReserveSlot("r1", 9:00, 10:00)
│     → adds (9:00, 10:00) to room's schedule
│
├─ Step 4: _bookings.Add(booking)
│
├─ Step 5: Notify observers
│     → NotificationService: sends email to all participants
│     → HistoryService: stores HistoryRecord
│
└─ return booking
```

### V1 Thread-Safety Issues (with examples)

#### Issue 1: CheckAvailability + ReserveSlot TOCTOU

```csharp
// V1: Two SEPARATE calls — gap between check and reserve
public Booking? BookRoom(...)
{
    if (!_roomManager.CheckAvailability(roomId, start, end))  // CHECK
        return null;
    // ═══════ GAP: another thread can reserve the same slot here ═══════
    var booking = builder.Build();
    _roomManager.ReserveSlot(roomId, start, end);             // USE (reserve)
    ...
}
```

```
Thread A: CheckAvailability("r1", 9:00, 10:00) → true (room is free)
Thread B: CheckAvailability("r1", 9:00, 10:00) → true (room STILL free — A hasn't reserved yet!)

Thread A: ReserveSlot("r1", 9:00, 10:00) → schedule now has [9:00-10:00]
Thread B: ReserveSlot("r1", 9:00, 10:00) → schedule now has [9:00-10:00, 9:00-10:00] ← DOUBLE BOOKED!

Result: Two bookings for the same room at the same time.
  Both threads thought the room was available because Check and Reserve are separate.
```

#### Issue 2: Schedule List Concurrent Modification

```csharp
// V1: plain List<(DateTime, DateTime)>
public void ReserveSlot(string roomId, DateTime start, DateTime end)
{
    schedule.Add((start, end)); // List.Add — not thread-safe
}
```

```
Thread A: ReserveSlot → schedule.Add(...) — modifying the list
Thread B: CheckAvailability → schedule.Any(...) — iterating the list

Result: InvalidOperationException "Collection was modified during enumeration"
```

#### Issue 3: Observer List Race

```
Thread A: BookRoom succeeds → foreach (var obs in _observers) obs.OnBookingCreated(...)
Thread B: bookingManager.AddObserver(newObserver) → _observers.Add(...)

Result: "Collection was modified during enumeration" crash
```

#### Issue 4: Singleton Race

```csharp
// V1: not thread-safe
public static RoomManager GetInstance()
{
    _instance ??= new RoomManager(); // two threads can both see null!
    return _instance;
}
```

```
Thread A: _instance is null → creates RoomManager #1
Thread B: _instance is null (before A's write visible) → creates RoomManager #2

Result: Two different instances — rooms added to #1 aren't visible in #2.
```

#### Issue 5: History List Race

```
Thread A: HistoryService.OnBookingCreated → _history.Add(record)
Thread B: historyService.GetHistory() → _history.ToList() (iterates)

Result: ConcurrentModification crash or corrupted list.
```

---

## V1 to V2

### What Changed

| Aspect | V1 | V2 |
|--------|----|----|
| Check + Reserve | Two separate calls (TOCTOU) | Atomic `CheckAndReserve()` under per-room lock |
| Lock granularity | None | Per-room lock (different rooms parallel) |
| Schedule storage | `Dictionary<string, List>` | `ConcurrentDictionary` + per-room lock object |
| Bookings list | `List` (crash) | `ImmutableList` + `ImmutableInterlocked` |
| Observer list | `List` (crash) | `ImmutableList` + snapshot iteration |
| History | `List` (crash) | `ImmutableList` |
| Singleton init | `??=` (race) | `lock` + double-check locking |

---

## V2 — Fully Thread-Safe

### V2 Class Diagram
![alt text](v2-cd.png)

### How V2 Became Thread-Safe

#### Fix 1: Atomic CheckAndReserve (eliminates TOCTOU)

```csharp
// V2: Check + Reserve in ONE lock acquisition
public bool CheckAndReserve(string roomId, DateTime start, DateTime end)
{
    if (!_schedules.TryGetValue(roomId, out var entry)) return false;

    lock (entry.lockObj) // Per-room lock
    {
        // CHECK: any overlap?
        bool hasConflict = entry.schedule.Any(s => start < s.end && end > s.start);
        if (hasConflict) return false;

        // RESERVE: add to schedule (ATOMIC with check — no gap)
        entry.schedule.Add((start, end));
        return true;
    }
}
```

**Why per-room lock (not global):**
- Different rooms should be bookable in parallel
- Only same-room bookings need serialization
- Global lock would be a bottleneck (all rooms blocked)

#### Fix 2: ImmutableList for Collections

```csharp
// V2: Bookings — safe concurrent add + iterate
private ImmutableList<Booking> _bookings = ImmutableList<Booking>.Empty;

// Add: creates new list, old references unaffected
ImmutableInterlocked.Update(ref _bookings, list => list.Add(booking));

// Iterate: snapshot is immutable, safe even if Add runs concurrently
var observers = _observers; // snapshot
foreach (var obs in observers) obs.OnBookingCreated(booking);
```

#### Fix 3: Thread-Safe Singletons

```csharp
// V2: Double-checked locking
private static RoomManager? _instance;
private static readonly object _singletonLock = new();

public static RoomManager GetInstance()
{
    if (_instance == null)           // fast path (no lock after init)
        lock (_singletonLock)        // only one thread creates
            _instance ??= new RoomManager();
    return _instance;
}
```

### V2 Concurrent Booking Race (Example)

```
Setup:
  Conference A: empty schedule
  Thread A: Book 9:00-10:00 (Alice's team meeting)
  Thread B: Book 9:00-10:00 (Bob's standup)

Timeline:

T1  Thread A: BookRoom(..., "r1", 9:00, 10:00)
    Thread B: BookRoom(..., "r1", 9:00, 10:00)

T2  Thread A: roomManager.CheckAndReserve("r1", 9:00, 10:00)
    Thread B: roomManager.CheckAndReserve("r1", 9:00, 10:00)

T3  Thread A: lock(r1.lockObj) ← ACQUIRED
    Thread B: lock(r1.lockObj) ← BLOCKED (waiting)

T4  Thread A (inside lock):
      schedule.Any(s => 9:00 < s.end && 10:00 > s.start) → false (empty)
      schedule.Add((9:00, 10:00)) ← RESERVED
      return true
    EXIT lock

T5  Thread B: lock(r1.lockObj) ← ACQUIRED
      schedule.Any(s => 9:00 < s.end && 10:00 > s.start) → TRUE!
        (9:00 < 10:00 && 10:00 > 9:00 → overlap with Thread A's booking)
      return false ← REJECTED
    EXIT lock

T6  Thread A: builder.Build(), add to bookings, notify observers → SUCCESS
    Thread B: "Room r1 NOT available" → FAILED

Result:
  Alice: BOOKED ✓
  Bob: REJECTED ✗ (no double-booking)
  Per-room lock ensured check + reserve was atomic.
```

### Different Rooms in Parallel

```
Thread A: CheckAndReserve("r1", ...) → lock(r1.lockObj) ← ACQUIRED
Thread B: CheckAndReserve("r2", ...) → lock(r2.lockObj) ← ACQUIRED (different lock!)

Both proceed simultaneously — no contention.
Different rooms never block each other.
```

### Decorator Chain Example

```
BookingBuilder(room, 9:00, 10:00, [Alice, Bob])
  .WithTV()
  .WithWhiteboard()
  .Build()

Build() creates:
  BasicRoom("Conference A")                          → "Conference A (Table & Chairs)"
    └─ TVFeature(basicRoom)                          → + " + TV"           cost: +50
        └─ WhiteboardFeature(tvFeature)              → + " + Whiteboard"   cost: +20

Final: "Conference A (Table & Chairs) + TV + Whiteboard"
Cost: 0 + 50 + 20 = $70
```

### Observer Flow After Booking

```
bookingManager.BookRoom(builder, "r1", 9:00, 10:00)
  │
  ├─ CheckAndReserve → success
  ├─ builder.Build() → Booking with decorators
  ├─ ImmutableInterlocked.Update(_bookings, add)
  │
  └─ Notify observers (snapshot iteration):
       ├─ NotificationService.OnBookingCreated(booking)
       │     → EmailNotification.Send(alice, "Meeting booked: Conference A (09:00-10:00)")
       │     → EmailNotification.Send(bob, "Meeting booked: Conference A (09:00-10:00)")
       │
       └─ HistoryService.OnBookingCreated(booking)
             → ImmutableInterlocked.Update(_history, add record)
             → "[History] Stored: Booking(...)"
```

### Thread-Safety Summary

```
Component         | V1 Issue              | V2 Fix
──────────────────┼───────────────────────┼─────────────────────────────────
Check+Reserve     | TOCTOU (two calls)    | Atomic CheckAndReserve (one lock)
Lock scope        | None                  | Per-room lock (parallel rooms OK)
Schedule list     | Plain List (crash)    | List under per-room lock
Bookings/History  | List (crash)          | ImmutableList + ImmutableInterlocked
Observer list     | List (crash)          | ImmutableList (snapshot iteration)
Singleton init    | ??= (race)            | lock + double-check locking
```
