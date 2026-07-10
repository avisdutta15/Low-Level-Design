# Notification Patterns

## Simple Notification (Observer Pattern — NotificationPatternsV1)

A basic pub-sub implementation using the Observer pattern. The subject (`ParkingLot`) maintains a list of observers and notifies them when events occur.

### IObserver Interface

```csharp
namespace NotificationPatternsV1.Observers;

public interface IObserver
{
    public void Update(string message);
}
```

### ISubject Interface

```csharp
using NotificationPatternsV1.Observers;

namespace NotificationPatternsV1.Subject;

public interface ISubject
{
    public void Subscribe(IObserver observer);
    public void Unsubscribe(IObserver observer);
    public void NotifyObservers(string message);
}
```

### Concrete Observers

```csharp
namespace NotificationPatternsV1.Observers;

public class ConsoleObserver : IObserver
{
    public void Update(string message)
    {
        Console.WriteLine($"Console Observer: {message}");
    }
}

public class DashboardObserver : IObserver
{
    public void Update(string message)
    {
        Console.WriteLine($"Dashboard observer: {message}");
    }
}
```

### ParkingLot (Subject)

```csharp
using NotificationPatternsV1.Observers;
using NotificationPatternsV1.Subject;

namespace NotificationPatternsV1;

public class ParkingLot : ISubject
{
    private readonly List<IObserver> _observers;

    public ParkingLot()
    {
        _observers = new List<IObserver>();
    }

    public void Subscribe(IObserver observer)
    {
        if(!_observers.Contains(observer))
            _observers.Add(observer);
    }

    public void Unsubscribe(IObserver observer)
    {
        if(_observers.Contains(observer))
            _observers.Remove(observer);
    }

    public void NotifyObservers(string message)
    {
        foreach (var observer in _observers)
        {
            observer.Update(message);
        }
    }

    public void ParkCar(string carModel)
    {
        NotifyObservers($"Car {carModel} has been parked.");
    }

    public void UnparkCar(string carModel)
    {
        NotifyObservers($"Car {carModel} has been unparked.");
    }
}
```

### Usage

```csharp
ParkingLot parkingLot = new();

IObserver consoleObserver = new ConsoleObserver();
IObserver dashboardObserver = new DashboardObserver();

parkingLot.Subscribe(consoleObserver);
parkingLot.Subscribe(dashboardObserver);

parkingLot.ParkCar("Toyota");
parkingLot.ParkCar("Maruti");
parkingLot.ParkCar("Hyundai");
```

This works fine in a single-threaded context. The problem begins when multiple threads interact with the same `ParkingLot` instance.

---

## Thread-Safety Issues in `ParkingLot.cs`

The `_observers` list is **not thread-safe**. Here's the breakdown:

**1. `Subscribe` / `Unsubscribe` — Race conditions on `List<IObserver>`**

`List<T>` is not thread-safe. Concurrent calls can corrupt internal state, cause duplicate additions (the `Contains` + `Add` aren't atomic), or throw `IndexOutOfRangeException`.

**2. `NotifyObservers` — Collection modified during enumeration**

If another thread calls `Subscribe`/`Unsubscribe` while `foreach` is iterating, you get `InvalidOperationException: Collection was modified`.

**3. `ConsoleObserver` / `DashboardObserver`**

Stateless, only call `Console.WriteLine` (internally thread-safe). No issues.

---

## Fix Options

### Option A — Simple lock

```csharp
private readonly object _lock = new();
private readonly List<IObserver> _observers = new();

public void Subscribe(IObserver observer)
{
    lock (_lock)
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }
}

public void Unsubscribe(IObserver observer)
{
    lock (_lock)
    {
        _observers.Remove(observer);
    }
}

public void NotifyObservers(string message)
{
    lock (_lock)
    {
        foreach (var observer in _observers)
        {
            observer.Update(message);
        }
    }
}
```

**When to choose this:**

Pick this when your system is simple — few observers, infrequent notifications, and you just need correctness without overthinking performance. The lock serializes everything: no two threads can subscribe, unsubscribe, or notify at the same time. This is the "get it right first" option.

**Why it works well in simple cases:** The cognitive overhead is minimal. There's one lock, one rule: hold it for any access to `_observers`. Deadlocks aren't a concern unless the observer's `Update` method tries to subscribe/unsubscribe (re-entrant call) — which would deadlock with `lock` since it's not re-entrant by default. Actually `lock` (Monitor) IS re-entrant in C#, so even that case is safe — but it can cause logical issues like modifying the list mid-iteration.

**Why NOT to choose this:** If observers do heavy work (network calls, database writes, file I/O), the lock is held for the entire notification loop. Every other thread trying to subscribe, unsubscribe, or even send a different notification is blocked. In high-throughput systems this becomes a bottleneck.

---

### Option B — Lock with snapshot

```csharp
private readonly object _lock = new();
private readonly List<IObserver> _observers = new();

public void Subscribe(IObserver observer)
{
    lock (_lock)
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }
}

public void Unsubscribe(IObserver observer)
{
    lock (_lock)
    {
        _observers.Remove(observer);
    }
}

public void NotifyObservers(string message)
{
    IObserver[] snapshot;
    lock (_lock)
    {
        snapshot = _observers.ToArray();
    }

    foreach (var observer in snapshot)
    {
        observer.Update(message);
    }
}
```

**When to choose this:**

Pick this when observers might be slow (I/O, external service calls) and you don't want notifications to block subscribe/unsubscribe. The lock is held only for the short time it takes to copy the array — microseconds — then released. Other threads can freely mutate the list while notifications are dispatched.

**Why it works well for slow observers:** Imagine you have 50 observers and some of them send HTTP webhooks. With Option A, a thread trying to subscribe a new observer must wait until all 50 webhooks complete. With snapshotting, it waits only for the `ToArray()` copy (nanoseconds for small lists).

**Trade-off to be aware of:** The snapshot is a point-in-time copy. If an observer unsubscribes after the snapshot is taken but before notification reaches it, it still gets notified for that round. This is usually acceptable (and is how most event systems work — "unsubscribe takes effect on the next event cycle") but if you need strict "never notify after unsubscribe" guarantees, you'd need a cancellation token or per-observer active flag.

**Why NOT to choose this:** The array allocation on every notify call adds GC pressure. In hot paths (thousands of notifications per second) with many observers, this allocation can add up. In those cases, Option C or D are better.

---

### Option C — Copy-on-write with `ImmutableList<T>`

```csharp
using System.Collections.Immutable;

private ImmutableList<IObserver> _observers = ImmutableList<IObserver>.Empty;

public void Subscribe(IObserver observer)
{
    ImmutableInterlocked.Update(ref _observers, list =>
        list.Contains(observer) ? list : list.Add(observer));
}

public void Unsubscribe(IObserver observer)
{
    ImmutableInterlocked.Update(ref _observers, list => list.Remove(observer));
}

public void NotifyObservers(string message)
{
    foreach (var observer in _observers) // snapshot semantics built-in
    {
        observer.Update(message);
    }
}
```

**When to choose this:**

Pick this when notifications happen far more frequently than subscribe/unsubscribe — which is the common case in most observer patterns. Think: a stock ticker with 10 observers that rarely change, but prices update thousands of times per second. Reads are completely lock-free; only writes pay the cost of creating a new list.

**Why it works well for read-heavy workloads:** `ImmutableList<T>` uses structural sharing (a balanced tree internally), so `Add`/`Remove` don't copy the entire list — they share most of the existing nodes. Reading threads never block. There's no lock, no contention, no possibility of deadlock during notification. The `foreach` in `NotifyObservers` iterates over a stable reference that won't change mid-loop even if another thread subscribes.

**How `ImmutableInterlocked.Update` works:** It uses a compare-and-swap (CAS) loop internally. It reads the current reference, applies your transformation function, then attempts an atomic swap. If another thread modified the reference between the read and swap, it retries. This is lock-free but not wait-free — under extreme write contention the retry loop can spin, but in practice subscribe/unsubscribe contention is low.

**Common Bug — How NOT to write `ImmutableInterlocked.Update`:**

```csharp
// ❌ BROKEN — observer is never actually added
ImmutableInterlocked.Update(ref _observers, (list) =>
{
    if (list.Contains(observer))
        return list;
    else
        list.Add(observer);  // ← returns a NEW list, original unchanged
        return list;         // ← always returns the original empty list
});
```

`ImmutableList<T>.Add(item)` does **not** mutate `list`. It returns a brand-new list with the item appended. The original `list` remains unchanged — that's the whole point of immutability. By discarding the return value of `.Add()` and returning the original `list`, the transformation function always returns the same reference it received. `ImmutableInterlocked.Update` sees that `original == updated` (same reference), skips the CAS entirely, and nothing is ever added.

The correct way:

```csharp
// ✅ CORRECT — return the new list from .Add()
ImmutableInterlocked.Update(ref _observers, list =>
    list.Contains(observer) ? list : list.Add(observer)
);
```

| Collection | `.Add()` behavior |
|------------|-------------------|
| `List<T>` (mutable) | Mutates in place, returns `void` |
| `ImmutableList<T>` (immutable) | Returns a new list, original is untouched |

This is a common trap when moving from mutable to immutable collections. The API forces you to capture the return value — if you don't, the modification is silently lost.

**Why NOT to choose this:** If subscribe/unsubscribe is frequent (e.g., observers come and go rapidly), the CAS retries and tree allocations add up. Also, `ImmutableList<T>.Contains` is O(n) — for very large observer lists, consider `ImmutableHashSet<T>` instead. Adds a NuGet dependency on `System.Collections.Immutable` (included in modern .NET, but worth noting for older frameworks).

---

### Option D — `ReaderWriterLockSlim`

```csharp
private readonly ReaderWriterLockSlim _rwLock = new();
private readonly List<IObserver> _observers = new();

public void Subscribe(IObserver observer)
{
    _rwLock.EnterWriteLock();
    try
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }
    finally
    {
        _rwLock.ExitWriteLock();
    }
}

public void Unsubscribe(IObserver observer)
{
    _rwLock.EnterWriteLock();
    try
    {
        _observers.Remove(observer);
    }
    finally
    {
        _rwLock.ExitWriteLock();
    }
}

public void NotifyObservers(string message)
{
    _rwLock.EnterReadLock();
    try
    {
        foreach (var observer in _observers)
        {
            observer.Update(message);
        }
    }
    finally
    {
        _rwLock.ExitReadLock();
    }
}
```

**When to choose this:**

Pick this when you have many threads calling `NotifyObservers` concurrently and subscribe/unsubscribe is rare. Unlike a plain `lock`, multiple threads can hold the read lock simultaneously — so 10 threads can all be notifying at the same time without blocking each other. Only a write (subscribe/unsubscribe) requires exclusive access.

**Why it works well for concurrent notifications:** In systems like real-time dashboards or event buses where many producers fire events simultaneously, a plain `lock` forces them into single-file. `ReaderWriterLockSlim` lets them all proceed in parallel as long as no one is modifying the list.

**Important subtlety — writer starvation vs. reader starvation:** `ReaderWriterLockSlim` by default does NOT favor writers. If readers constantly hold the lock, a writer might wait indefinitely. In practice, since subscribe/unsubscribe is infrequent, this isn't usually a problem. But if you have bursts of subscriptions, be aware of this.

**Why NOT to choose this:** It's heavier than a plain `lock` — `ReaderWriterLockSlim` has more internal bookkeeping. For small observer lists with cheap `Update` methods, the overhead of `EnterReadLock`/`ExitReadLock` can actually be slower than a plain `lock`. Also, it's `IDisposable`, so your `ParkingLot` class needs to implement `IDisposable` too. And if an observer's `Update` tries to subscribe/unsubscribe, you'll deadlock — `ReaderWriterLockSlim` is NOT re-entrant by default (you can enable recursion via `LockRecursionPolicy.SupportsRecursion`, but Microsoft discourages it due to complexity).

---

### Option E — Parallel notification dispatch with tasks. Fire and forget

```csharp
private readonly object _lock = new();
private readonly List<IObserver> _observers = new();

public void Subscribe(IObserver observer)
{
    lock (_lock)
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }
}

public void Unsubscribe(IObserver observer)
{
    lock (_lock)
    {
        _observers.Remove(observer);
    }
}

public async Task NotifyObserversAsync(string message)
{
    IObserver[] snapshot;
    lock (_lock)
    {
        snapshot = _observers.ToArray();
    }

    foreach (var observer in _observers) 
    {
        Task.Run(() =>
        {
            try
            {
                observer.Update(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        });
    }
}
```

Or synchronous with `Parallel.ForEach`:

```csharp
public void NotifyObservers(string message)
{
    IObserver[] snapshot;
    lock (_lock)
    {
        snapshot = _observers.ToArray();
    }

    Parallel.ForEach(snapshot, (observer) => {
        try{
            observer.Update(message);
        }catch (Exception ex){
            Console.WriteLine(ex.ToString());
        }
    });
}
```

**When to choose this:**

Pick this when observer work is expensive and independent — each observer does something heavy (sends an email, writes to a database, calls an external API) and they don't depend on each other's results. Parallel dispatch can dramatically reduce total notification time from `sum(all observer times)` to `max(single observer time)`.

**Why it works well for I/O-bound observers:** If you have 5 observers each making a 200ms HTTP call, sequential dispatch takes ~1 second. Parallel dispatch takes ~200ms. For systems where notification latency matters (real-time alerts, live dashboards), this is a significant improvement.

**`Task.Run` vs `Parallel.ForEach`:** Use `Task.Run` + `Task.WhenAll` when observers are I/O-bound (awaiting network, disk) — this doesn't block thread pool threads. Use `Parallel.ForEach` when observers are CPU-bound (heavy computation) — it uses the thread pool efficiently with work-stealing. For mixed workloads, `Task.Run` is safer.

**Why NOT to choose this:** For cheap observers (like `Console.WriteLine`), the overhead of scheduling tasks far exceeds the work itself — you'll actually be slower. Also, parallel dispatch introduces ordering concerns: observers receive and process notifications in unpredictable order. If observer B depends on observer A having processed first, this breaks. Error handling is also more complex — you need to decide whether one observer's failure should affect others (with `Task.WhenAll`, all tasks run; exceptions are aggregated in `AggregateException`).

---

## Decision Guide

| Situation | Best Option | Why |
|-----------|-------------|-----|
| Simple app, few observers, cheap updates | A (simple lock) | Least complexity, hard to get wrong |
| Observers do slow work, subscriptions happen during notify | B (snapshot) | Doesn't block subscribe/unsubscribe during dispatch |
| Notifications are extremely frequent, subscriptions are rare | C (copy-on-write) | Lock-free reads, zero contention on notify path |
| Many threads notify concurrently, mutations are rare | D (ReaderWriterLockSlim) | Parallel reads without snapshot allocation overhead |
| Observer work is heavy/I/O-bound, latency matters | E (parallel dispatch) | Reduces total notification time to slowest single observer |

In most real-world observer patterns, **Option B or C** covers 90% of cases. Start with B if you want simplicity with good performance characteristics, move to C if profiling shows lock contention on the notify path, and consider E if observer latency is the bottleneck.
