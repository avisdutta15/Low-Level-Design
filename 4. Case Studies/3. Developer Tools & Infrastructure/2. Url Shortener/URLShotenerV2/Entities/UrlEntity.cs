namespace URLShotenerV2.Entities;

public class UrlEntity
{
    public string OriginalUrl { get; }              // No set as they are always set via constructor
    public string ShortUrl { get; }                 // No set as they are always set via constructor
    public DateTimeOffset? ExpirationTime { get; }  // No set as they are always set via constructor
    public DateTimeOffset? LastAccessed { get; private set; }   // This will be set by RecordVisit

    private int _totalClicks;
    public int TotalClicks => _totalClicks;


    public UrlEntity(string originalUrl, string shortUrl, DateTimeOffset? expirationTime = null)
    {
        if (string.IsNullOrWhiteSpace(originalUrl)) throw new ArgumentException("Original URL cannot be empty.");
        if (string.IsNullOrWhiteSpace(shortUrl)) throw new ArgumentException("Alias cannot be empty.");

        OriginalUrl = originalUrl;
        ShortUrl = shortUrl;
        ExpirationTime = expirationTime;
        _totalClicks = 0;
        LastAccessed = null;
    }

    public void RecordVisit()
    {
        Interlocked.Increment(ref _totalClicks);
        LastAccessed = DateTimeOffset.UtcNow;
    }

    public bool IsExpired()
    {
        return ExpirationTime.HasValue && ExpirationTime.Value < DateTimeOffset.UtcNow;
    }
}