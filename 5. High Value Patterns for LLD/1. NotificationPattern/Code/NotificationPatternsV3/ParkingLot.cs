using NotificationPatternsV3.Observers;
using NotificationPatternsV3.Subject;

namespace NotificationPatternsV3;

/*
    The problem: With single lock across all the Subscribe, Unsubscribe and Notify path
                 makes the system slow.
    Imagine you have 50 observers and some of them send HTTP webhooks. 
    With single lock across all operations, a thread trying to subscribe a new observer 
    must wait until all 50 webhooks complete. 
    
    The Fix!
    Snapshotting pattern! Snapshot the _observers list before iterating and sending notification.
    List<IObserver> snapshot;
    lock(_observerLock)
    {
        snapshot = _observers.ToArray();
    }
    With snapshotting, it waits only for the ToArray() copy (nanoseconds for small lists).

    Trade-off to be aware of: 
    The snapshot is a point-in-time copy. If an observer unsubscribes after the snapshot is 
    taken but before notification reaches it, it still gets notified for that round. 
    This is usually acceptable (and is how most event systems work — "unsubscribe takes effect 
    on the next event cycle") but if you need strict "never notify after unsubscribe" guarantees, 
    you'd need a cancellation token or per-observer active flag.

    Why NOT to choose this: The array allocation on every notify call adds GC pressure. 
    In hot paths (thousands of notifications per second) with many observers, this allocation can add up. 
    In those cases, Version 4 and 5 are better.
 */

public class ParkingLot : ISubject
{
    private readonly List<IObserver> _observers;
    private readonly object _lock = new object();

    public ParkingLot()
    {
        _observers = new List<IObserver>();
    }

    public void Subscribe(IObserver observer)
    {
        // Reader threads (NotifyObservers) waits till the writer thread is done and lock is free
        lock (_lock)
        {
            if (!_observers.Contains(observer))
                _observers.Add(observer);
        }
    }

    public void Unsubscribe(IObserver observer)
    {
        // Reader threads (NotifyObservers) waits till the writer thread is done and lock is free
        lock (_lock)
        {
            if (_observers.Contains(observer))
                _observers.Remove(observer);
        }
    }

    public void NotifyObservers(string message)
    {
        List<IObserver> snapshot;
        lock (_lock)
        {
            // Create a snapshot of the observers list to avoid issues
            // with modification during iteration. A new array is allocated which is 
            // a copy of _observers. If NotifyObservers is a hot-path then the GC
            // pressure increases. Check the Diagonostic Tools while running this program.
            snapshot = _observers.ToList();
        }

        foreach (var observer in snapshot)
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
