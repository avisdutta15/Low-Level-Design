namespace _1.URLShotenerV1.Entities;

public class UrlEntity
{
    public string OriginalUrl { get; }      // No set as they are always set via constructor
    public string ShortUrl { get; }
    public DateTimeOffset? ExpirationTime { get; }
    public DateTimeOffset CreatedDate { get; }
    public DateTimeOffset? LastAccessed { get; private set; }

    private int _totalClicks;
    public int TotalClicks => _totalClicks;

    public UrlEntity(string originalUrl, string shortUrl, DateTimeOffset? expirationTime)
    {
        OriginalUrl = originalUrl;
        ShortUrl = shortUrl;
        ExpirationTime =  expirationTime;
        _totalClicks = 0;
        LastAccessed = null;
    }

    public void RecordVisit()
    {
        _totalClicks++;
        LastAccessed = DateTimeOffset.UtcNow;
    }

    public bool IsExpired() 
    {
        return ExpirationTime.HasValue && ExpirationTime.Value < DateTimeOffset.UtcNow;
    }
}
