namespace Observer.V1;

/// <summary>
/// Without Observer: The storage service directly calls every dependent system.
/// 
/// Problems:
///   - Tight coupling: FileStorageService knows about Logger, SearchIndex, NotificationService, AuditTrail
///   - Adding a new subscriber (e.g., MetricsCollector) = modifying this class
///   - Removing a subscriber = modifying this class
///   - Violates OCP: not open for extension without modification
///   - Violates SRP: storage service handles upload + notification orchestration
///   - Hard to test: must mock all subscribers to test upload logic
/// </summary>
public class FileStorageService
{
    private readonly LoggingService _logger;
    private readonly SearchIndexService _search;
    private readonly NotificationService _notification;
    private readonly AuditTrailService _audit;

    public FileStorageService(
        LoggingService logger,
        SearchIndexService search,
        NotificationService notification,
        AuditTrailService audit)
    {
        _logger = logger;
        _search = search;
        _notification = notification;
        _audit = audit;
    }

    public void Upload(string fileName, byte[] content, string author)
    {
        // Core responsibility: store the file
        Console.WriteLine($"  [Storage] Uploading '{fileName}' ({content.Length} bytes)");

        // Now manually notify every dependent system — TIGHT COUPLING
        _logger.Log($"File '{fileName}' uploaded by {author}");
        _search.IndexFile(fileName, author);
        _notification.SendUploadAlert(fileName, author);
        _audit.RecordUpload(fileName, author, DateTime.UtcNow);
    }

    public void Delete(string fileName, string author)
    {
        Console.WriteLine($"  [Storage] Deleting '{fileName}'");

        // Must remember to notify EVERY system for EVERY action
        _logger.Log($"File '{fileName}' deleted by {author}");
        _search.RemoveFile(fileName);
        _notification.SendDeletionAlert(fileName, author);
        _audit.RecordDeletion(fileName, author, DateTime.UtcNow);
    }
}

public class LoggingService
{
    public void Log(string message) => Console.WriteLine($"  [Log] {message}");
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
    public void SendUploadAlert(string fileName, string author)
        => Console.WriteLine($"  [Notify] Upload alert: '{fileName}' by {author}");
    public void SendDeletionAlert(string fileName, string author)
        => Console.WriteLine($"  [Notify] Deletion alert: '{fileName}' by {author}");
}

public class AuditTrailService
{
    public void RecordUpload(string fileName, string author, DateTime time)
        => Console.WriteLine($"  [Audit] Upload recorded: '{fileName}' at {time:HH:mm:ss}");
    public void RecordDeletion(string fileName, string author, DateTime time)
        => Console.WriteLine($"  [Audit] Deletion recorded: '{fileName}' at {time:HH:mm:ss}");
}
