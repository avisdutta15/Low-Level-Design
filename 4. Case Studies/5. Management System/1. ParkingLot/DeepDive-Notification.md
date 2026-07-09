# Deep Dive: Observer Notification in a Concurrent Parking Lot

---

## Phase 1 — Simple Observer (Per Vehicle Type Counts)

A straightforward observer that notifies subscribers with available spot counts broken down by vehicle type.

### Observer Interface

```csharp
public interface IParkingObserver
{
    void Update(Dictionary<VehicleType, int> availableByType);
}
```

### Subject Interface

```csharp
public interface IParkingSubject
{
    void Subscribe(IParkingObserver observer);
    void Unsubscribe(IParkingObserver observer);
    void Notify();
}
```

### ParkingFloor — Count by Type

```csharp
public Dictionary<VehicleType, int> AvailableSpotsByType()
{
    var result = new Dictionary<VehicleType, int>();
    foreach (var spot in _parkingSpots.Values)
    {
        if (!spot.IsOccupied())
        {
            if (result.ContainsKey(spot.VehicleType))
                result[spot.VehicleType]++;
            else
                result[spot.VehicleType] = 1;
        }
    }
    return result;
}
```

### ParkingLot — Aggregate and Notify

```csharp
private readonly List<IParkingObserver> _observers = new();

public void Subscribe(IParkingObserver observer)
{
    if (!_observers.Contains(observer))
        _observers.Add(observer);
}

public void Unsubscribe(IParkingObserver observer)
{
    _observers.Remove(observer);
}

public void Notify()
{
    var combined = new Dictionary<VehicleType, int>();

    foreach (var floor in _parkingFloors.Values)
    {
        foreach (var kvp in floor.AvailableSpotsByType())
        {
            if (combined.ContainsKey(kvp.Key))
                combined[kvp.Key] += kvp.Value;
            else
                combined[kvp.Key] = kvp.Value;
        }
    }

    foreach (var observer in _observers)
        observer.Update(combined);
}
```

### Observers

```csharp
public class DisplayBoard : IParkingObserver
{
    private readonly string _boardId;

    public DisplayBoard(string boardId) => _boardId = boardId;

    public void Update(Dictionary<VehicleType, int> availableByType)
    {
        Console.WriteLine($"[DisplayBoard {_boardId}]");
        foreach (var kvp in availableByType)
            Console.WriteLine($"  {kvp.Key}: {kvp.Value} spots available");
    }
}

public class MobileApp : IParkingObserver
{
    private readonly string _userId;

    public MobileApp(string userId) => _userId = userId;

    public void Update(Dictionary<VehicleType, int> availableByType)
    {
        Console.WriteLine($"[MobileApp {_userId}] Push notification:");
        foreach (var kvp in availableByType)
            Console.WriteLine($"  {kvp.Key}: {kvp.Value} spots available");
    }
}
```

This works perfectly in a single-threaded context. But when multiple threads park/unpark concurrently, it breaks.

---

## Phase 2 — Making the Observer Pattern Thread-Safe

### The Problem

`List<IParkingObserver>` is not thread-safe. Three race conditions exist:

1. **Concurrent Subscribe/Unsubscribe** — Two threads mutating the list simultaneously can corrupt its internal array or throw.
2. **Iterate during mutation** — `Notify()` iterates `_observers` via `foreach`. If another thread calls `Subscribe` or `Unsubscribe` during iteration, .NET throws `InvalidOperationException` ("Collection was modified during enumeration").
3. **Subscribe + Notify race** — An observer could receive a partial notification or be skipped entirely.

### Fix: Lock with Snapshot Iteration

```csharp
private readonly List<IParkingObserver> _observers = new();
private readonly object _observerLock = new();

public void Subscribe(IParkingObserver observer)
{
    lock (_observerLock)
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }
}

public void Unsubscribe(IParkingObserver observer)
{
    lock (_observerLock)
    {
        _observers.Remove(observer);
    }
}

public void Notify()
{
    var combined = new Dictionary<VehicleType, int>();

    foreach (var floor in _parkingFloors.Values)
    {
        foreach (var kvp in floor.AvailableSpotsByType())
        {
            if (combined.ContainsKey(kvp.Key))
                combined[kvp.Key] += kvp.Value;
            else
                combined[kvp.Key] = kvp.Value;
        }
    }

    // Take a snapshot under lock, then iterate outside the lock
    List<IParkingObserver> snapshot;
    lock (_observerLock)
    {
        snapshot = new List<IParkingObserver>(_observers);
    }

    foreach (var observer in snapshot)
        observer.Update(combined);
}
```

### Why Snapshot Outside Lock?

- We don't want to hold `_observerLock` while calling `observer.Update(...)` — the observer callback could be slow, or could itself try to subscribe/unsubscribe (deadlock).
- The snapshot is a point-in-time copy of the subscriber list. New subscribers added after the snapshot won't receive this notification, but will get the next one. This is fine.

### Alternative: ImmutableList with CAS (Lock-Free Reads)

If you want zero lock contention on the read path:

```csharp
private ImmutableList<IParkingObserver> _observers = ImmutableList<IParkingObserver>.Empty;

public void Subscribe(IParkingObserver observer)
{
    ImmutableList<IParkingObserver> current, updated;
    do
    {
        current = _observers;
        if (current.Contains(observer)) return;
        updated = current.Add(observer);
    } while (Interlocked.CompareExchange(ref _observers, updated, current) != current);
}

public void Unsubscribe(IParkingObserver observer)
{
    ImmutableList<IParkingObserver> current, updated;
    do
    {
        current = _observers;
        updated = current.Remove(observer);
    } while (Interlocked.CompareExchange(ref _observers, updated, current) != current);
}

public void Notify()
{
    var combined = new Dictionary<VehicleType, int>();
    foreach (var floor in _parkingFloors.Values)
    {
        foreach (var kvp in floor.AvailableSpotsByType())
        {
            if (combined.ContainsKey(kvp.Key))
                combined[kvp.Key] += kvp.Value;
            else
                combined[kvp.Key] = kvp.Value;
        }
    }

    // No lock needed — reading an immutable reference is always safe
    var observers = _observers;
    foreach (var observer in observers)
        observer.Update(combined);
}
```

This mirrors the CAS philosophy used in `ParkingSpot.TryOccupy()`.

### What's Still Not Consistent?

The observer list is now safe. But the **aggregation loop** (iterating floors, reading spot states) has no global lock. Between reading Floor 1 and Floor 2, a spot could change. The `combined` dictionary may reflect a state that never existed at any single instant.

For a display board, this is acceptable — eventual consistency. The next park/unpark triggers another `Notify()` that corrects it. But if you needed the count to be a true point-in-time snapshot...

---

## Phase 3 — Point-in-Time Consistent Snapshot

### The Problem

While `Notify()` aggregates counts across floors, another thread could be parking or unparking a vehicle. The result: Floor 1 count reflects time T₁, Floor 2 count reflects time T₂ where T₁ ≠ T₂. The combined total may be a value that never actually existed.

### Solution: ReaderWriterLockSlim

The idea: while you're reading counts, no one can mutate spot state. Multiple readers can coexist, but a writer (park/unpark) blocks until all readers finish, and vice versa.

```csharp
private readonly ReaderWriterLockSlim _rwLock = new();
```

#### Writers (ParkVehicle / UnParkVehicle) take a write lock:

```csharp
public Ticket? ParkVehicle(Vehicle vehicle)
{
    _rwLock.EnterWriteLock();
    try
    {
        foreach (var floor in _parkingFloors.Values)
        {
            var spot = floor.BookParkingSpot(vehicle);
            if (spot != null)
            {
                Ticket ticket = new Ticket(DateTime.Now, vehicle, floor.Id, spot.Id);
                _activeTickets.AddOrUpdate(
                    key: ticket.Id,
                    addValue: ticket,
                    updateValueFactory: (key, oldValue) => ticket
                );
                Console.WriteLine($"Vehicle parked. Ticket: {ticket.Id}");
                return ticket;
            }
        }
    }
    finally
    {
        _rwLock.ExitWriteLock();
    }

    // Notify OUTSIDE the write lock to avoid holding it during observer callbacks
    Notify();

    Console.WriteLine($"No spot available for vehicle type: {vehicle.Type}");
    return null;
}
```

#### Notify takes a read lock for the aggregation:

```csharp
public void Notify()
{
    var combined = new Dictionary<VehicleType, int>();

    _rwLock.EnterReadLock();
    try
    {
        foreach (var floor in _parkingFloors.Values)
        {
            foreach (var kvp in floor.AvailableSpotsByType())
            {
                if (combined.ContainsKey(kvp.Key))
                    combined[kvp.Key] += kvp.Value;
                else
                    combined[kvp.Key] = kvp.Value;
            }
        }
    }
    finally
    {
        _rwLock.ExitReadLock();
    }

    // Dispatch outside the lock
    List<IParkingObserver> snapshot;
    lock (_observerLock)
    {
        snapshot = new List<IParkingObserver>(_observers);
    }

    foreach (var observer in snapshot)
        observer.Update(combined);
}
```

### Why ReaderWriterLockSlim Over a Plain Lock?

- Multiple `Notify()` calls (readers) can run **in parallel** — they don't block each other.
- A `ParkVehicle`/`UnParkVehicle` (writer) waits until all current readers finish, then gets exclusive access.
- This gives you a consistent snapshot: while you're aggregating, no spot can transition between occupied/free.

### Tradeoff Comparison

| Approach | Consistency | Throughput | Use When |
|----------|------------|------------|----------|
| No lock on reads (Phase 2) | Eventual — counts may be slightly stale | Maximum — readers never block | Display boards, notifications |
| `ReaderWriterLockSlim` (Phase 3) | Point-in-time snapshot | Writers block during reads and vice versa | Reservation systems, capacity decisions |
| Global mutex | Point-in-time snapshot | Worst — everything serializes | Rarely appropriate |

### Important Note on CAS Redundancy

With the `ReaderWriterLockSlim` approach, the CAS on `ParkingSpot.TryOccupy()` becomes redundant for correctness — the write lock already serializes mutations. You could simplify `ParkingSpot._occupied` to a plain `bool` at that point. Keeping CAS doesn't hurt (it's a no-op under exclusive access), but it's no longer the correctness mechanism.

### When to Use Which

- **Phase 2 (lock on observer list only):** The right choice for most parking lot systems. Notifications are informational. The CAS on `ParkingSpot` is the real concurrency gate — it guarantees no double-booking regardless of what the display shows.

- **Phase 3 (ReaderWriterLockSlim):** Use when the aggregated count drives decisions — e.g., a reservation API that needs to guarantee "we have N spots" before confirming a booking. The consistent snapshot ensures the decision is based on reality, not a stale view.
