using URLShotenerV2.Entities;
using URLShotenerV2.Enums;

namespace URLShotenerV2.Observers;

public interface ISubject
{
    void Subscribe(IObserver observer);
    void Unsubscribe(IObserver observer);    
    void Notify(UrlEventType eventType, UrlEntity entity);
}
