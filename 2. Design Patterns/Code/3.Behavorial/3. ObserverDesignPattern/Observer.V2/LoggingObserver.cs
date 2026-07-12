namespace Observer.V2;

public class LoggingObserver : IStorageObserver
{
    public void OnStorageEvent(StorageEvent e)
    {
        Console.WriteLine($"  [Log] {e.EventType}: '{e.FileName}' by {e.Author} at {e.Timestamp:HH:mm:ss}");
    }
}
