namespace AbstractFactory.V2;

public class SqliteMetadataRepository : IMetadataRepository
{
    public void Save(string key, Dictionary<string, string> metadata)
        => Console.WriteLine($"  [SQLite] Saving metadata for key '{key}'");

    public Dictionary<string, string>? Get(string key)
    {
        Console.WriteLine($"  [SQLite] Fetching metadata for key '{key}'");
        return new Dictionary<string, string> { ["source"] = "sqlite" };
    }

    public void Delete(string key)
        => Console.WriteLine($"  [SQLite] Deleting metadata for key '{key}'");
}
