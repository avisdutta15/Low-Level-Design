using NotificationPatternsV4.Observers;

namespace NotificationPatternsV4.Subject;

public interface ISubject
{
    public void Subscribe(IObserver observer);
    public void Unsubscribe(IObserver observer);
    public void NotifyObservers(string message);
}
