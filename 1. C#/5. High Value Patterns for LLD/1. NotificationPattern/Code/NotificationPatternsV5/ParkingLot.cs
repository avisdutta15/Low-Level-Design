using NotificationPatternsV5.Observers;
using NotificationPatternsV5.Subject;
using System.Collections.Immutable;

namespace NotificationPatternsV5;

/*
    The problem: With single lock across all the Subscribe, Unsubscribe and Notify path
                 makes the system slow.
    Imagine you have 50 observers and some of them send HTTP webhooks. 
    With single lock across all operations, a thread trying to subscribe a new observer 
    must wait until all 50 webhooks complete. 
    
    The Fix!
    ReaderWriterLockSlim - Unlike a plain lock, multiple threads can hold the read lock simultaneously 
    — so 10 threads can all be notifying at the same time without blocking each other. 
    Only a write (subscribe/unsubscribe) requires exclusive access.

    writer starvation vs. reader starvation: ReaderWriterLockSlim by default does NOT favor writers. 
    If readers constantly hold the lock, a writer might wait indefinitely. 
    In practice, since subscribe/unsubscribe is infrequent, this isn't usually a problem. 
    But if you have bursts of subscriptions, be aware of this.

    Why NOT to choose this: It's heavier than a plain lock — ReaderWriterLockSlim has more internal bookkeeping. 
    For small observer lists with cheap Update methods, the overhead of EnterReadLock/ExitReadLock can actually 
    be slower than a plain lock. Also, it's IDisposable, so your ParkingLot class needs to implement IDisposable too. 
    And if an observer's Update tries to subscribe/unsubscribe, you'll deadlock — ReaderWriterLockSlim is 
    NOT re-entrant by default (you can enable recursion via LockRecursionPolicy.SupportsRecursion, 
    but Microsoft discourages it due to complexity).
 */

public class ParkingLot : ISubject
{
    private ImmutableList<IObserver> _observers = ImmutableList<IObserver>.Empty;
    private readonly object _lock = new object();

    private readonly ReaderWriterLockSlim _readerWriterLockSlim = new ReaderWriterLockSlim();

    public ParkingLot()
    {
    }

    // Write Path
    public void Subscribe(IObserver observer)
    {
        /*
        // Optimization 1
        lock (_lock)
        {
            if (!_observers.Contains(observer))
                _observers.Add(observer);
        }
        */

        /*
        // Optimization 2
        ImmutableInterlocked.Update(ref _observers, (list) =>
        {
            return list.Contains(observer) ? list : list.Add(observer);
        });
        */

        // Optimization 3
        _readerWriterLockSlim.EnterWriteLock();
        try
        {
            if (!_observers.Contains(observer))
                _observers.Add(observer);
        }
        finally 
        { 
            _readerWriterLockSlim.ExitWriteLock();
        }        
    }

    // Write Path
    public void Unsubscribe(IObserver observer)
    {
        /*
        // Optimization 1
        lock (_lock)
        {
            if (_observers.Contains(observer))
                _observers.Remove(observer);
        }
        */

        /*
        // Optimization 2
        ImmutableInterlocked.Update(ref _observers, (list) =>
        { 
            return list.Remove(observer); 
        });
        */

        // Optimization 3
        _readerWriterLockSlim.EnterWriteLock();
        try
        {
            _observers.Remove(observer);
        }
        finally
        {
            _readerWriterLockSlim.ExitWriteLock();
        }        
    }

    // Read Path
    public void NotifyObservers(string message)
    {
        /* 
        // Optimization 1
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

        /*
        // Optimization 2
        // We donot need the lock + snapshotting any more.
        // snapshot semantic is inbuilt in Immutable collections
        foreach (var observer in _observers)
        {
            observer.Update(message);
        }
        */

        // Optimization 3
        _readerWriterLockSlim.EnterReadLock();
        try
        {
            foreach (var observer in _observers)
            {
                observer.Update(message);
            }
        }
        finally
        {
            _readerWriterLockSlim.ExitReadLock();
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
