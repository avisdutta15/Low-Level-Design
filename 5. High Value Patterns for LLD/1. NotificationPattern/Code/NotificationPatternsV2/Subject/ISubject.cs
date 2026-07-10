using NotificationPatternsV2.Observers;

namespace NotificationPatternsV2.Subject;

public interface ISubject
{
    public void Subscribe(IObserver observer);
    public void Unsubscribe(IObserver observer);
    public void NotifyObservers(string message);
}
