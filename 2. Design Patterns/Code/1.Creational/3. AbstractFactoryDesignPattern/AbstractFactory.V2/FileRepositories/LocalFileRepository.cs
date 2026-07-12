namespace AbstractFactory.V2;

public class LocalFileRepository : IFileRepository
{
    public void Upload(string fileName, byte[] content)
        => Console.WriteLine($"  [Local] Writing '{fileName}' to local disk ({content.Length} bytes)");

    public byte[] Download(string fileName)
    {
        Console.WriteLine($"  [Local] Reading '{fileName}' from local disk");
        return Array.Empty<byte>();
    }

    public void Delete(string fileName)
        => Console.WriteLine($"  [Local] Deleting '{fileName}' from local disk");
}
