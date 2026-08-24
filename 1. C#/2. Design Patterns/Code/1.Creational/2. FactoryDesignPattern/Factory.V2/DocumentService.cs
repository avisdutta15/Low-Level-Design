namespace Factory.V2;

/// <summary>
/// Client class — uses the factory to create a file repository.
/// 
/// Notice: This class has ZERO knowledge of S3FileRepository, LocalFileRepository, etc.
/// It only depends on IFileRepository (abstraction) and FileRepositoryFactory.
/// 
/// Benefits:
///   - Single Responsibility: Service handles logic, Factory handles creation
///   - Open/Closed: New storage providers don't require changes here
///   - Testability: Factory can be mocked to return test doubles
/// </summary>
public class DocumentService
{
    private readonly IFileRepository _repository;

    public DocumentService(FileRepositoryFactory factory, StorageType storageType)
    {
        // Client doesn't know or care which concrete class is created
        _repository = factory.CreateRepository(storageType);
    }

    public void UploadDocument(string fileName, byte[] content)
    {
        Console.WriteLine($"  Uploading document: {fileName}");
        _repository.Upload(fileName, content);
    }

    public void DownloadDocument(string fileName)
    {
        Console.WriteLine($"  Downloading document: {fileName}");
        _repository.Download(fileName);
    }

    public void DeleteDocument(string fileName)
    {
        Console.WriteLine($"  Deleting document: {fileName}");
        _repository.Delete(fileName);
    }
}
