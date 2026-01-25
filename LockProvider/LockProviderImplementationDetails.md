# Lock Provider Implementation
## Overview
This document details the `InMemoryLockProvider` implementation to ensure thread-safety, correctness, and efficient resource management.
- `bool TryLock(string key, string userId, long ttlMs)` : Returns true if the provider was successful in locking the given key with the TTL in milliseconds.
- `void Unlock(string key)` : Unlock a given key
- `bool IsLockExpired(string key)` : Returns true if the lock is expired for the given key
- `bool IsLockedBy(string key, string userId)` : Returns true if the key is locked by the given user

To implement and store the lock we will make use of 2 data structures:
- A custom `Lock` object.
- A `ConcurrentDictionary<string, Lock>` representing {key, Lock}


## Concurrent Dictionary
The ConcurrentDictionary has the following APIs:
- **TryAdd(key, value)** : Attempts to add the specified key and value to the ConcurrentDictionary<TKey,TValue>.
- **AddOrUpdate(key, Func<key, Value, Value>, Func<key, Value, Value>)** : Uses the specified functions to add a key/value pair to the ConcurrentDictionary<TKey,TValue> if the key does not already exist, or to update a key/value pair in the ConcurrentDictionary<TKey,TValue> if the key already exists.
- **AddOrUpdate(key, value, Func<key, Value, Value>)** : Adds a key/value pair to the ConcurrentDictionary<TKey,TValue> if the key does not already exist, or updates a key/value pair in the ConcurrentDictionary<TKey,TValue> by using the specified function if the key already exists.
- **TryRemove(key, out value)** : Attempts to remove and return the value that has the specified key from the ConcurrentDictionary<TKey,TValue>.
- **TryUpdate(key, newValue, comparisonValue)** : Updates the value associated with key to newValue if the existing value with key is equal to comparisonValue.

**Notes:**
If we try to add a key to the collection if not present or update a value of a key, then we should not use TryAdd() and TryUpdate(). Instead use AddOrUpdate().


## Custom Lock Object
This will be implemented by a record type.
```csharp
private record Lock(DateTime Expiry, string OwnerId)
```
**Why record and not class or struct?**

Using a `record` (specifically a `record class`) instead of a standard `class` or `struct` provides three critical benefits for concurrent programming: **Immutability, Value-Based Equality, and Conciseness**.

Here is why record is the superior choice for our `Lock` object.

**1. Immutability by Default (Thread Safety)**
In a multi-threaded environment, mutable objects inside a shared collection are dangerous.

- **The Risk with Classes:** A standard `class` usually has `get; set;` properties. If Thread A retrieves the object and Thread B changes the `ExpiresAt` property of that same object while it sits in the dictionary, you have a race condition that `ConcurrentDictionary` cannot prevent.

- **The Record Advantage:** Positional records are immutable (`init` only) by default. Once created, the data cannot change. To "change" it, you must replace the entire object in the dictionary. This aligns perfectly with the atomic `AddOrUpdate` model.

**2. Value-Based Equality (The "Superpower")**
This is the most technical benefit specifically for the Safe Removal logic.

- **Standard Class (Reference Equality)**: Two different instances of a class are considered "Not Equal," even if they contain the exact same data.
```csharp
var a = new MyClass("User1");
var b = new MyClass("User1");
bool match = (a == b); // FALSE (Different memory addresses)
```
- **Record (Value Equality):** Records automatically generate `Equals` and `GetHashCode` methods based on their properties.
```csharp
var a = new MyRecord("User1");
var b = new MyRecord("User1");
bool match = (a == b); // TRUE (Data is identical)
```

**3. Debugging and Logging**
Records automatically generate a readable `ToString()` method.
- **Class:** `System.Collections.Generic.KeyValuePair...` (Useless in logs)
- **Record:** `Lock { ExpiresAt = 12:00:00, OwnerId = "User1" }` (Instantly readable)

This is invaluable when debugging race conditions in logs.

**Why not struct?**

You might think struct is faster. However, `ConcurrentDictionary<K, V>` works best with reference types for `V`. If you use a large `struct`, the runtime has to copy the entire struct every time you retrieve it, pass it to a method, or check it. Since `Lock` is small, it's not a huge deal, but `record` gives you the efficiency of passing references with the safety of value semantics.


## Adding a Lock Object to the Concurrent Dictionary

The initial implementation had a critical race condition where multiple threads could incorrectly believe they acquired the same lock.

**Original Code:**
```csharp
public bool TryLock(string key, string userId, long ttlMs)
{
    DateTime now = DateTime.UtcNow;
    var newLock = new Expiry(ExpiresAt: now.AddMilliseconds(ttlMs), OwnerId: userId);
    _lockCache.AddOrUpdate(key, newLock, (key, oldLock) =>
    {
        if (oldLock.ExpiresAt > now) 
            return oldLock;
        return newLock;
    });

    //Check if we were successful in setting the lock.
    //If successful then the newLock and the lock in the Dictionary will match.
    if (_lockCache.TryGetValue(key, out var _lock) && _lock == newLock)
    {
        return true;
    }
    return false;
}
```

**Problem:** After `AddOrUpdate` completes, another thread could modify the dictionary value before the `TryGetValue` check, causing incorrect lock acquisition results.

### Solution: ReferenceEquals Pattern
```csharp
public bool TryLock(string key, string userId, long ttlMs)
{
    DateTime now = DateTime.UtcNow;
    var newLock = new Expiry(ExpiresAt: now.AddMilliseconds(ttlMs), OwnerId: userId);
    var effectiveLock = _lockCache.AddOrUpdate(key, newLock, (key, oldLock) =>
    {
        if(oldLock.ExpiresAt > now)
        {
            return oldLock;
        }
        return newLock;
    });

    return ReferenceEquals(newLock, effectiveLock);
}
```

**Why This Works:**
- `ReferenceEquals` checks if `newLock` and `effectiveLock` are the exact same object instance
- Only the thread whose `newLock` instance was stored in the dictionary will get `true`
- Provides atomic lock acquisition guarantee without additional synchronization
- Leverages the fact that records are reference types with distinct identities



## Memory: Zombie Entries

In an in-memory implementation, keys that expire are effectively "dead," but they remain in the `ConcurrentDictionary` consuming memory until someone calls `TryLock` on that specific key again. If you have millions of unique keys, this is a memory leak. Fixing this requires a background cleanup timer.

Two Approaches:

- **Timer Based**

  Key Implementation Details:

    - **The Timer:** We use `System.Threading.Timer` to trigger a cleanup every few minutes.

    - **Atomic Removal:** The cleanup process must be careful. We cannot just say "If key is expired, delete it."

    **Race Condition:** Between checking if it's expired and deleting it, a new valid lock might be registered for that same key.

    **Solution:** We use the `ICollection<KeyValuePair<...>>.Remove` interface explicitly implemented by `ConcurrentDictionary`. This allows us to say "Remove this key only if it still has this exact expired value."

```csharp
using System.Collections.Concurrent;

public class InMemoryLockProvider : ILockProvider, IDisposable
{
    // Records provide value-based equality, which is crucial for atomic removal
    private record Lock(DateTime ExpiresAt, string OwnerId);
    private readonly ConcurrentDictionary<string, Lock> _lockCache;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5); // Run cleanup every 5 mins

    public InMemoryLockProvider()
    {
        _lockCache = new();
        
        // Start a background timer to clean up zombies
        _cleanupTimer = new Timer(RemoveZombieLocks, null, _cleanupInterval, _cleanupInterval);
    }

    private void RemoveZombieLocks(object? state)
    {
        var now = DateTime.UtcNow;

        // Iterate over the KeyValuePairs in the dictionary
        foreach (var kvp in _lockCache)
        {
            // 1. Check if expired
            if (kvp.Value.ExpiresAt <= now)
            {
                // 2. Atomic Removal using ICollection explicit implementation.
                // We pass the specific KVP we found. If the dictionary has changed
                // (e.g., someone updated the lock 1ms ago), this Remove will fail safely.
                ((ICollection<KeyValuePair<string, Expiry>>)_lockCache).Remove(kvp);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }

    public bool TryLock(string key, string userId, long ttlMs) { ... }

    public void Unlock(string key) { ... }
    
    public void Unlock(string key, string userId) { ... }

    public bool IsLockedBy(string key, string userId) { ... }

    public bool IsLockExpired(string key) { ... }
}
```

- **Periodic Timer Based (more modern)**

  Instead of a heavy `Timer` infrastructure, we can use a simple "Fire-and-Forget" background task in the constructor. This keeps the class self-contained and much easier to read.
  This version handles Zombie Entries using a modern `PeriodicTimer` (or a simple loop) running in the background.
```csharp
using System.Collections.Concurrent;

public class InMemoryLockProvider : ILockProvider
{
    private record Lock(DateTime ExpiresAt, string OwnerId);
    private readonly ConcurrentDictionary<string, Lock> _lockCache;

    public InMemoryLockProvider()
    {
        _lockCache = new();

        // Start a background cleanup task immediately (Fire and Forget)
        // This runs automatically to clean up zombies every 1 minute.
        Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (await timer.WaitForNextTickAsync())
            {
                CleanupZombies();
            }
        });
    }

    private void CleanupZombies()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _lockCache)
        {
            if (kvp.Value.ExpiresAt <= now)
            {
                // Safe Removal: effectively says "Delete this key ONLY if 
                // the value hasn't changed since I last looked at it."
                ((ICollection<KeyValuePair<string, Lock>>)_lockCache).Remove(kvp);
            }
        }
    }

    public bool TryLock(string key, string userId, long ttlMs) { ... }

    public void Unlock(string key) { ... }
    
    public void Unlock(string key, string userId) { ... }

    public bool IsLockedBy(string key, string userId) { ... }

    public bool IsLockExpired(string key) { ... }    
}
```
-------
**What is the difference between the following**?

```csharp
Task.Run(async () => 
{
    var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
    while (await timer.WaitForNextTickAsync())
    {
        CleanUpZombieLocks();
    }
});
```
and

```csharp
Task.Run(async () =>
{
    while (true)
    {
        CleanUpZombieLocks();
        await Task.Delay(TimeSpan.FromMinutes(2));
    }
});
```

Both approaches achieve the goal of running a background task repeatedly, but `PeriodicTimer` is designed specifically to solve the hidden flaw of the while(true) + Task.Delay pattern: `Time Drift`.

Here is the breakdown of the differences.

**1. Timing Stability (Drift vs. Fixed Interval)**

- **Task.Delay (The Drifter):** The delay starts after your work finishes. If `CleanUpZombieLocks()` takes **10 seconds** to run, your actual interval becomes **2 minutes + 10 seconds**. Over an hour, your cleanup might run **28 times** instead of **30**. The schedule "drifts" later and later.

- **PeriodicTimer (The Metronome):** This is designed to "tick" at fixed intervals, regardless of how long the work takes. If the timer is set for **2 minutes** and your work takes **10 seconds**, the timer waits only **1 minute 50 seconds** for the next tick. It keeps the heartbeat steady.

**2. Handling Long Execution (Overrun)**

What happens if `CleanUpZombieLocks()` accidentally takes **3 minutes** (longer than the interval)?

- **Task.Delay:** It doesn't care. It waits for the **3-minute** job to finish, then waits another **2 minutes**. The gap between starts becomes **5 minutes**.

- **PeriodicTimer:** It will not overlap runs concurrently. If the job takes too long, `WaitForNextTickAsync` will return true immediately for the next iteration to help catch up (or maintain the pace), ensuring the average frequency stays as close to the target as possible without concurrent overlap.

**3. Cancellation Handling**

- **Task.Delay:** You often need messy `try/catch` blocks for `TaskCanceledException` inside your loop if you want to stop it cleanly.

- **PeriodicTimer:** It is designed for clean stopping. If you dispose the timer or pass a cancellation token to `WaitForNextTickAsync`, the loop exits cleanly and naturally, returning false.


**What is the difference between `((ICollection<KeyValuePair<string, Expiry>>)_lockCache).Remove(kvp)` and `_lockCache.TryRemove(kvp.Key, out _)`?**

Here is the deep dive into how that specific line of code works and why it provides the "magic" thread safety you need.

**1. The Syntax: Explicit Interface Implementation**

You might wonder why you can't just type `_lockCache.Remove(kvp)`. `ConcurrentDictionary` implements the standard `ICollection<KeyValuePair<...>>` interface, but it implements it explicitly. This is a C# design choice to hide methods that might be confusing or slower if used incorrectly. By casting to `ICollection`, you unlock access to this specific overload of `Remove`.

**2. The Logic: "Compare-and-Swap" (CAS)**

When you call `((ICollection...)dict).Remove(item)`, you are executing a strictly defined conditional operation.

It does not simply say "Delete key X". It says: "Delete key X, but ONLY IF the value inside currently matches Y."

Here is the step-by-step internal execution:

- **Locking**: The dictionary enters a synchronized state (it locks the specific "bucket" where this key lives).

- **Lookup**: It finds the entry for kvp.Key.

- **Comparison**: It compares the current value in memory against kvp.Value.

    - Since you used a record, it uses **Value Equality** (checking all properties like Deadline and OwnerId).

    - If you used a standard class, it would use `Reference Equality`.

- **Decision**:

    - **Match:** It removes the item.

    - **No Match:** It does nothing and returns false.

- **Unlocking:** It releases the lock.

Because steps 2 through 4 happen inside the same lock, no other thread can modify the item in the middle of this process.

**3. Visualizing the Difference**

The standard `TryRemove` leaves a "gap" where errors occur. The `ICollection.Remove` is a sealed, atomic block.

**4. Simplified Internal Code**

If we were to look at a simplified version of the source code for `ConcurrentDictionary`, it would look something like this:

```csharp
// PSEUDO-CODE of what happens inside .NET
bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
{
    TKey key = item.Key;
    TValue expectedValue = item.Value;

    // 1. ACQUIRE LOCK for this specific key hash
    lock (GetBucketLock(key)) 
    {
        // 2. GET CURRENT
        if (_dictionary.TryGetValue(key, out TValue currentValue))
        {
            // 3. COMPARE (Crucial Step!)
            // It uses the default equality comparer (e.g., record properties)
            if (EqualityComparer<TValue>.Default.Equals(currentValue, expectedValue))
            {
                // 4. REMOVE
                _dictionary.RemoveInternal(key);
                return true; // Success! We killed the zombie.
            }
        }
    } 
    // LOCK RELEASED

    // If we get here, it means either the key was gone, 
    // OR the value had changed (someone updated the lock).
    // We return false and touch nothing.
    return false; 
}
```
**5. Why Records Matter Here**

In your code, `Lock` is a record.
```csharp
private record Lock(DateTime ExpiresAt, string OwnerId);
```
This makes the cleanup logic robust. Even if the internal object reference has changed (e.g., some internal copying mechanism), as long as the data (`ExpiresAt` and `OwnerId`) is exactly the same, the equality check passes, and the zombie is removed.

If you used a standard class without overriding `.Equals()`, the atomic remove would be even stricter: it would only remove the item if it was the exact same instance in memory. For a lock provider, either works, but record is often safer for reasoning about data states.


**In short:**

**The Problem: The ABA Problem during Cleanup**

When the background cleaner finds an expired key, it cannot simply delete it.
- Risk: Between detecting the expiration and calling `Remove`, a user thread might update that key with a valid lock. A standard `TryRemove(key)` would delete the user's active lock.

**The Solution: Atomic Conditional Remove**

We utilized the `ICollection<KeyValuePair<TKey, TValue>>.Remove` explicit implementation. This performs an atomic "Compare-and-Swap" (CAS) operation.

```csharp
// BEFORE (Dangerous)
// blindly deletes key regardless of value
_locks.TryRemove(key, out _); 

// AFTER (Safe)
// deletes key ONLY IF value matches the expired entry we found
((ICollection<KeyValuePair<string, Lock>>)_locks).Remove(kvp);
```
___________________

## Performance Considerations

### Time Complexity
- `TryLock`: O(1) average case
- `Unlock`: O(1)
- `IsLockedBy`: O(1)
- `IsLockExpired`: O(1)

### Space Complexity
- O(n) where n is number of active locks
- With lazy cleanup: O(n + m) where m is expired but not yet cleaned locks
- With background cleanup: Bounded by cleanup frequency

### Optimization Tips
- Use appropriate TTL values to minimize expired key accumulation
- Consider background cleanup for high-throughput scenarios
- Monitor dictionary size in production

