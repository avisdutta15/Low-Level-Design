namespace Facade.V1;

public class SearchIndexService
{
    public void IndexDocument(string documentId, string content, Dictionary<string, string> metadata)
        => Console.WriteLine($"  [Search] Indexed '{documentId}' with {metadata.Count} metadata fields");

    public List<string> Search(string query)
    {
        Console.WriteLine($"  [Search] Searching for '{query}'");
        return new List<string> { "doc-001", "doc-002" };
    }

    public void RemoveFromIndex(string documentId)
        => Console.WriteLine($"  [Search] Removed '{documentId}' from index");
}
