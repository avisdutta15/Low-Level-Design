namespace AbstractFactory.V2;

/// <summary>
/// Client class — uses the Abstract Factory to get storage repositories.
/// 
/// This class has ZERO knowledge of S3, DynamoDB, SQLite, etc.
/// It only depends on:
///   - IStorageFactory (the abstract factory)
///   - IFileRepository, IMetadataRepository, ISearchIndex (abstract products)
/// 
/// You can swap the entire storage backend by passing a different factory.
/// </summary>
public class DocumentService
{
    private readonly IFileRepository _fileRepo;
    private readonly IMetadataRepository _metadataRepo;
    private readonly ISearchIndex _searchIndex;

    public DocumentService(IStorageFactory factory)
    {
        // All repositories come from the SAME factory → guaranteed consistency
        _fileRepo = factory.CreateFileRepository();
        _metadataRepo = factory.CreateMetadataRepository();
        _searchIndex = factory.CreateSearchIndex();
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
