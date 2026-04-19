using URLShotenerV2.Entities;

namespace URLShotenerV2.Observers;

public interface IUrlObserver
{
    void OnUrlEvent(UrlEventType eventType, UrlEntity entity);
}
