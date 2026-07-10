using NotificationPatternsV2.Observers;
using NotificationPatternsV2.Subject;

namespace NotificationPatternsV2;

/*
    The problem: The List<IObserver> _observers is not thread-safe.
    This can lead to race conditions and data corruption when multiple threads
    The Fix - 
    1. Use a lock statement to synchronize access to the _observers list.
    2. Use Thread-Safe Collections like ConcurrentBag<T> or ConcurrentQueue<T> from System.Collections.Concurrent.
 
    Pick this when your system is simple — few observers, infrequent notifications, 
    and you just need correctness without overthinking performance. 
    The lock serializes everything: no two threads can subscribe, unsubscribe, or notify 
    at the same time. This is the "get it right first" option.

    Why NOT to choose this: If observers do heavy work (network calls, database writes, file I/O), 
    the lock is held for the entire notification loop. 
    Every other thread trying to subscribe, unsubscribe, or even send a different notification 
    is blocked. In high-throughput systems this becomes a bottleneck.
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
        // Writer threads (Subscribe and Unsubscribe) waits till the notification is complete and lock is free
        lock (_lock)
        {
            foreach (var observer in _observers)
            {
                observer.Update(message);
            }
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
