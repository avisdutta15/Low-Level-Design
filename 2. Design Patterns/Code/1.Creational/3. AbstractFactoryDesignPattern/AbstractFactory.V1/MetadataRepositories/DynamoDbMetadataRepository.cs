namespace AbstractFactory.V1;

public class DynamoDbMetadataRepository : IMetadataRepository
{
    public void Save(string key, Dictionary<string, string> metadata)
        => Console.WriteLine($"  [DynamoDB] Saving metadata for key '{key}'");

    public Dictionary<string, string>? Get(string key)
    {
        Console.WriteLine($"  [DynamoDB] Fetching metadata for key '{key}'");
        return new Dictionary<string, string> { ["source"] = "dynamodb" };
    }

    public void Delete(string key)
        => Console.WriteLine($"  [DynamoDB] Deleting metadata for key '{key}'");
}
