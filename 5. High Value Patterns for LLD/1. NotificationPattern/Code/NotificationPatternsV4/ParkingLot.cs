using NotificationPatternsV4.Observers;
using NotificationPatternsV4.Subject;
using System.Collections.Immutable;

namespace NotificationPatternsV4;

/*
    The problem: With single lock across all the Subscribe, Unsubscribe and Notify path
                 makes the system slow.
    Imagine you have 50 observers and some of them send HTTP webhooks. 
    With single lock across all operations, a thread trying to subscribe a new observer 
    must wait until all 50 webhooks complete. 
    
    The Fix!
    Copy-on-write with ImmutableList<T>
    Pick this when notifications happen far more frequently than subscribe/unsubscribe
    
    Why it works well for read-heavy workloads: 
    ImmutableList<T> uses structural sharing (a balanced tree internally), 
    so Add/Remove don't copy the entire list — they share most of the existing nodes. 
    Reading threads never block. There's no lock, no contention, no possibility of deadlock 
    during notification. 
    The foreach in NotifyObservers iterates over a stable reference that won't change mid-loop 
    even if another thread subscribes.
    
    How ImmutableInterlocked.Update works: It uses a compare-and-swap (CAS) loop internally. 
    It reads the current reference, applies your transformation function, then attempts an 
    atomic swap. If another thread modified the reference between the read and swap, it retries. 
    This is lock-free but not wait-free — under extreme write contention the retry loop can spin, 
    but in practice subscribe/unsubscribe contention is low.

    Why NOT to choose this: If subscribe/unsubscribe is frequent (e.g., observers come and 
    go rapidly), the CAS retries and tree allocations add up. 
    Also, ImmutableList<T>.Contains is O(n) — for very large observer lists, 
    consider ImmutableHashSet<T> instead. 
 */

public class ParkingLot : ISubject
{
    private ImmutableList<IObserver> _observers = ImmutableList<IObserver>.Empty;
    private readonly object _lock = new object();

    public ParkingLot()
    {
    }

    // Write Path
    public void Subscribe(IObserver observer)
    {
        // Optimization 1
        /*
        lock (_lock)
        {
            if (!_observers.Contains(observer))
                _observers.Add(observer);
        }
        */

        // Optimization 2
        ImmutableInterlocked.Update(ref _observers, (list) =>
        {
            return list.Contains(observer) ? list : list.Add(observer);
        });

        /*
            for Immutable collection, every operation returns a new collection.
            list.Add(item) returns a new list.
            if you write
            // Bug :
            ImmutableInterlocked.Update(ref _observers, (list) => {
                if (list.Contains(observer))
                    return list;
                else
                    list.Add(observer);  // ← returns a NEW list, original unchanged
                return list;             // ← always returns the original empty list
            });
            The original list remains unchanged (that's the whole point of immutability).
            By discarding the return value of .Add() and returning the original list, 
            the transformation function always returns the same reference it received. 
            ImmutableInterlocked.Update sees that original == updated (same reference), 
            skips the CAS entirely, and nothing is ever added.
        */
    }

    // Write Path
    public void Unsubscribe(IObserver observer)
    {
        // Optimization 1
        /*
        lock (_lock)
        {
            if (_observers.Contains(observer))
                _observers.Remove(observer);
        }
        */

        // Optimization 2
        ImmutableInterlocked.Update(ref _observers, (list) =>
        { 
            return list.Remove(observer); 
        });
    }

    // Read Path
    public void NotifyObservers(string message)
    {
        // Optimization 1
        /*
        List<IObserver>  snapshot;
        lock (_lock)
        {
            // Create a snapshot of the observers list to avoid issues
            // with modification during iteration. A new array is allocated which is 
            // a copy of _observers. If NotifyObservers is a hot-path then the GC
            // pressure increases. Check the Diagonostic Tools while running this program.
            snapshot = _observers.ToList();
        }
        */

        // Optimization 2
        // We donot need the lock + snapshotting any more.
        // snapshot semantic is inbuilt in Immutable collections
        foreach (var observer in _observers)
        {
            observer.Update(message);
        }
    }

    public void ParkCar(string carModel)
    {
        //.....

        NotifyObservers($"Car {carModel} has been parked.");
    }

    public void UnparkCar(string carModel)
    {
        //.....

        NotifyObservers($"Car {carModel} has been unparked.");
    }
}
