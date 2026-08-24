namespace Mediator.V1;

/// <summary>
/// Without Mediator: Every component knows about and directly calls other components.
/// This creates N×N dependencies — each component is coupled to every other.
/// </summary>

public class FileStorageService
{
    private readonly QuotaService _quota;
    private readonly SearchIndexService _search;
    private readonly NotificationService _notification;

    public FileStorageService(QuotaService quota, SearchIndexService search, NotificationService notification)
    {
        _quota = quota;
        _search = search;
        _notification = notification;
    }

    public bool Upload(string fileName, byte[] content, string author)
    {
        // Must check quota BEFORE uploading — knows about QuotaService
        if (!_quota.HasSpace(content.Length))
        {
            Console.WriteLine($"  [Storage] REJECTED: Quota exceeded");
            _notification.SendAlert($"Upload failed for '{fileName}' — quota exceeded");
            return false;
        }

        Console.WriteLine($"  [Storage] Uploading '{fileName}' ({content.Length} bytes)");
        _quota.ConsumeSpace(content.Length);
        _search.IndexFile(fileName, author);
        _notification.SendAlert($"'{fileName}' uploaded by {author}");
        return true;
    }

    public void Delete(string fileName, long fileSize)
    {
        Console.WriteLine($"  [Storage] Deleting '{fileName}'");
        _quota.ReleaseSpace(fileSize);
        _search.RemoveFile(fileName);
        _notification.SendAlert($"'{fileName}' deleted");
    }
}

public class QuotaService
{
    private readonly NotificationService _notification;
    private long _usedBytes;
    private readonly long _maxBytes;

    public QuotaService(NotificationService notification, long maxBytes = 100 * 1024 * 1024)
    {
        _notification = notification;
        _maxBytes = maxBytes;
    }

    public bool HasSpace(long bytes) => (_usedBytes + bytes) <= _maxBytes;

    public void ConsumeSpace(long bytes)
    {
        _usedBytes += bytes;
        Console.WriteLine($"  [Quota] Used: {_usedBytes}/{_maxBytes} bytes");

        if (_usedBytes > _maxBytes * 0.9)
            _notification.SendAlert("WARNING: Storage quota > 90%!");
    }

    public void ReleaseSpace(long bytes)
    {
        _usedBytes -= bytes;
        Console.WriteLine($"  [Quota] Released {bytes} bytes. Used: {_usedBytes}/{_maxBytes}");
    }
}

public class SearchIndexService
{
    public void IndexFile(string fileName, string author)
        => Console.WriteLine($"  [Search] Indexed '{fileName}' by {author}");

    public void RemoveFile(string fileName)
        => Console.WriteLine($"  [Search] Removed '{fileName}' from index");
}

public class NotificationService
{
    public void SendAlert(string message)
        => Console.WriteLine($"  [Notify] {message}");
}
