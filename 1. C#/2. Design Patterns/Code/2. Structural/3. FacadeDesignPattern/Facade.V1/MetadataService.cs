namespace Facade.V1;

public class MetadataService
{
    public void SaveMetadata(string documentId, string author, long sizeBytes, string contentType)
        => Console.WriteLine($"  [Metadata] Saved: id='{documentId}', author='{author}', size={sizeBytes}, type='{contentType}'");

    public Dictionary<string, string> GetMetadata(string documentId)
    {
        Console.WriteLine($"  [Metadata] Fetching metadata for '{documentId}'");
        return new Dictionary<string, string>
        {
            ["author"] = "Alice",
            ["contentType"] = "application/pdf"
        };
    }

    public void DeleteMetadata(string documentId)
        => Console.WriteLine($"  [Metadata] Deleting metadata for '{documentId}'");
}
