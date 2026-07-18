using URLShotenerV2.Entities;
using URLShotenerV2.Enums;

namespace URLShotenerV2.Observers;

public interface IObserver
{
    void Update(UrlEventType eventType, UrlEntity entity);
}
