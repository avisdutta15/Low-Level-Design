using NotificationPatternsV5.Observers;

namespace NotificationPatternsV5.Subject;

public interface ISubject
{
    public void Subscribe(IObserver observer);
    public void Unsubscribe(IObserver observer);
    public void NotifyObservers(string message);
}
