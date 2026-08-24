namespace Observer.V2;

public class NotificationObserver : IStorageObserver
{
    public void OnStorageEvent(StorageEvent e)
    {
        Console.WriteLine($"  [Notify] Alert: '{e.FileName}' was {e.EventType.ToLower()} by {e.Author}");
    }
}
