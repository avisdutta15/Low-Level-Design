namespace AbstractFactory.V1;

/// <summary>
/// Client class WITHOUT Abstract Factory.
/// 
/// Problems:
///   - Knows about EVERY concrete class (S3, DynamoDB, Local, SQLite, etc.)
///   - Environment switch logic is INSIDE the service — violates SRP
///   - Adding a new environment means modifying THIS class
///   - Adding a new repository type means modifying THIS class
///   - Nothing prevents mixing repos from different environments
///   - Hard to unit test — can't inject mocks without refactoring
/// </summary>
public class DocumentService
{
    private readonly IFileRepository _fileRepo;
    private readonly IMetadataRepository _metadataRepo;
    private readonly ISearchIndex _searchIndex;

    public DocumentService(string environment)
    {
        // The service itself decides which concrete classes to use — BAD!
        // This couples the service to ALL implementations across ALL environments.
        if (environment == "production")
        {
            _fileRepo = new S3FileRepository();
            _metadataRepo = new DynamoDbMetadataRepository();
            _searchIndex = new ElasticSearchIndex();
        }
        else if (environment == "development")
        {
            _fileRepo = new LocalFileRepository();
            _metadataRepo = new SqliteMetadataRepository();
            _searchIndex = new InMemorySearchIndex();
        }
        else
        {
            throw new ArgumentException($"Unknown environment: {environment}");
        }
    }

    public void UploadDocument(string fileName, byte[] content, string author)
    {
        Console.WriteLine($"  Uploading document: {fileName}");
        _fileRepo.Upload(fileName, content);
        _metadataRepo.Save(fileName, new Dictionary<string, string>
        {
            ["author"] = author,
            ["uploadedAt"] = DateTime.UtcNow.ToString("O")
        });
        _searchIndex.Index(fileName, $"Document by {author}");
    }

    public void SearchDocuments(string query)
    {
        Console.WriteLine($"  Searching for: {query}");
        var results = _searchIndex.Search(query);
        Console.WriteLine($"  Found {results.Count} result(s)");
    }

    public void DeleteDocument(string fileName)
    {
        Console.WriteLine($"  Deleting document: {fileName}");
        _fileRepo.Delete(fileName);
        _metadataRepo.Delete(fileName);
    }
}
