using NotificationPatternsV1.Observers;
using NotificationPatternsV1.Subject;

namespace NotificationPatternsV1;

/*
    The problem: The _observers list is not thread-safe. Here's the full breakdown:
    1. Subscribe / Unsubscribe — Race conditions on List<IObserver>
        List<T> is not thread-safe. If two threads call Subscribe or Unsubscribe concurrently, 
        you can get:
        - The Contains check and Add are not atomic
        - Corrupted internal array state. Missing Updates
        - Missed removals or IndexOutOfRangeException
        - ConcurrentModificationException

    2. NotifyObservers — Collection modified during enumeration
        If one thread is iterating in NotifyObservers (via foreach) while another thread 
        calls Subscribe or Unsubscribe, you'll get an 
        InvalidOperationException: Collection was modified.
 */
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
        //.....

        NotifyObservers($"Car {carModel} has been parked.");
    }

    public void UnparkCar(string carModel)
    {
        //.....

        NotifyObservers($"Car {carModel} has been unparked.");
    }
}
