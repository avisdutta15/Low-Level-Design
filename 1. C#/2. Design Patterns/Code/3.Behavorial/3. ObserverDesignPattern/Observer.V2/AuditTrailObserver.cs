namespace Observer.V2;

public class AuditTrailObserver : IStorageObserver
{
    public void OnStorageEvent(StorageEvent e)
    {
        Console.WriteLine($"  [Audit] {e.EventType} recorded: '{e.FileName}' by {e.Author} at {e.Timestamp:HH:mm:ss}");
    }
}
