using NotificationPatternsV6.Observers;
using NotificationPatternsV6.Subject;
using System.Collections.Immutable;

namespace NotificationPatternsV6;

/*
    The problem: With single lock across all the Subscribe, Unsubscribe and Notify path
                 makes the system slow.
    Imagine you have 50 observers and some of them send HTTP webhooks. 
    With single lock across all operations, a thread trying to subscribe a new observer 
    must wait until all 50 webhooks complete. 
    
    The Fix!
    Copy-On-Write + Individual threads for each observer.
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
        //_readerWriterLockSlim.EnterWriteLock();
        //try
        //{
        //    if (!_observers.Contains(observer))
        //        _observers.Add(observer);
        //}
        //finally 
        //{ 
        //    _readerWriterLockSlim.ExitWriteLock();
        //}
        //

        // Optimization 4
        ImmutableInterlocked.Update(ref _observers, (list) =>
        {
            return list.Contains(observer) ? list : list.Add(observer);
        });
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
        //_readerWriterLockSlim.EnterWriteLock();
        //try
        //{
        //    _observers.Remove(observer);
        //}
        //finally
        //{
        //    _readerWriterLockSlim.ExitWriteLock();
        //}
        //

        // Optimization 4
        ImmutableInterlocked.Update(ref _observers, (list) =>
        {
            return list.Remove(observer);
        });
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
        //_readerWriterLockSlim.EnterReadLock();
        //try
        //{
        //    foreach (var observer in _observers)
        //    {
        //        observer.Update(message);
        //    }
        //}
        //finally
        //{
        //    _readerWriterLockSlim.ExitReadLock();
        //}

        // Optimization 4
        // Create individual thread to notify each notifier
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
        
        // Or
        //Parallel.ForEach(_observers, observer => observer.Update(message);
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
