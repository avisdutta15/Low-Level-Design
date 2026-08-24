using NotificationPatternsV1.Observers;

namespace NotificationPatternsV1.Subject;

public interface ISubject
{
    public void Subscribe(IObserver observer);
    public void Unsubscribe(IObserver observer);
    public void NotifyObservers(string message);
}
