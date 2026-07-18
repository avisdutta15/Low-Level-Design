using URLShotenerV1.Entities;

namespace URLShotenerV1.Observers;

public interface IObserver
{
    void Update(UrlEventType eventType, UrlEntity entity);
}
