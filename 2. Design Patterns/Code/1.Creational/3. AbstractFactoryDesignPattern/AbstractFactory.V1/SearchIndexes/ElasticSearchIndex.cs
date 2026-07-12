namespace AbstractFactory.V1;

public class ElasticSearchIndex : ISearchIndex
{
    public void Index(string documentId, string content)
        => Console.WriteLine($"  [ElasticSearch] Indexing document '{documentId}'");

    public List<string> Search(string query)
    {
        Console.WriteLine($"  [ElasticSearch] Searching for '{query}'");
        return new List<string> { "result-from-elasticsearch" };
    }
}
