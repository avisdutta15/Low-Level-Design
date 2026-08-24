namespace Facade.V2;

public class NotificationService
{
    public void NotifyUpload(string documentId, string author)
        => Console.WriteLine($"  [Notify] Sent upload notification: '{documentId}' by {author}");

    public void NotifyDeletion(string documentId)
        => Console.WriteLine($"  [Notify] Sent deletion notification: '{documentId}'");
}
