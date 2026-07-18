using URLShotenerV1.Entities;

namespace URLShotenerV1.Observers;

public interface ISubject
{
    void Subscribe(IObserver observer);
    void Unsubscribe(IObserver observer);
    void Notify(UrlEventType eventType, UrlEntity entity);
}
