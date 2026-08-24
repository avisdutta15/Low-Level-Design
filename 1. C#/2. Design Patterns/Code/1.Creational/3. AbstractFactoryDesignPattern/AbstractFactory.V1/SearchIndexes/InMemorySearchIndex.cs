namespace AbstractFactory.V1;

public class InMemorySearchIndex : ISearchIndex
{
    public void Index(string documentId, string content)
        => Console.WriteLine($"  [InMemory] Indexing document '{documentId}'");

    public List<string> Search(string query)
    {
        Console.WriteLine($"  [InMemory] Searching for '{query}'");
        return new List<string> { "result-from-memory" };
    }
}
