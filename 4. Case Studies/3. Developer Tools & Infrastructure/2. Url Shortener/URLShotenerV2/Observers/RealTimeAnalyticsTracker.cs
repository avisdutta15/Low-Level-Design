using System.Collections.Concurrent;
using URLShotenerV2.Entities;

namespace URLShotenerV2.Observers;

public class RealTimeAnalyticsTracker : IUrlObserver
{
    // O(1) Interlocked counters for extreme performance
    private int _totalLinks;
    private int _totalClicks;

    // A lightweight shadow-map just to track expiration dates for the ActiveLinks metric
    private readonly ConcurrentDictionary<string, DateTimeOffset?> _expirations;

    public RealTimeAnalyticsTracker()
    {
        _expirations = new();
    }

    public void OnUrlEvent(UrlEventType eventType, UrlEntity entity)
    {
        switch (eventType)
        {
            case UrlEventType.UrlCreated:
                Interlocked.Increment(ref _totalLinks);
                _expirations.TryAdd(entity.ShortUrl, entity.ExpirationTime);
                break;
            case UrlEventType.UrlVisited:
                Interlocked.Increment(ref _totalClicks);
                break;
        }
    }

    public SystemAnalytics GetAnalytics()
    {
        int active = 0;
        var now = DateTimeOffset.UtcNow;

        // Calculate active links lazily from the lightweight dictionary
        foreach (var exp in _expirations.Values)
        {
            if (!exp.HasValue || exp.Value > now)
            {
                active++;
            }
        }

        return new SystemAnalytics(_totalLinks, _totalClicks, active);
    }
}

