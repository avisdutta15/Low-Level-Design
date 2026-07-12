namespace Observer.V2;

/// <summary>
/// A NEW observer — added WITHOUT modifying FileStorageService.
/// This is the OCP benefit — extend behavior by adding new observers.
/// </summary>
public class MetricsObserver : IStorageObserver
{
    private int _uploadCount;
    private long _totalBytesUploaded;

    public void OnStorageEvent(StorageEvent e)
    {
        if (e.EventType == "Uploaded")
        {
            _uploadCount++;
            _totalBytesUploaded += e.FileSizeBytes;
            Console.WriteLine($"  [Metrics] Uploads: {_uploadCount}, Total bytes: {_totalBytesUploaded}");
        }
    }
}
