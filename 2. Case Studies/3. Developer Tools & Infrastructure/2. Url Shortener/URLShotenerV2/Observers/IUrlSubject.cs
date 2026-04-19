using URLShotenerV2.Entities;

namespace URLShotenerV2.Observers;

public interface IUrlSubject
{
    void Attach(IUrlObserver observer);
    void Detach(IUrlObserver observer);    
    void NotifyObservers(UrlEventType eventType, UrlEntity entity);
}
