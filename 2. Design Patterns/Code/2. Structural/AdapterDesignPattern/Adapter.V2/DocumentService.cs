namespace Adapter.V2;

/// <summary>
/// Client — depends ONLY on IFileRepository.
/// Doesn't know or care whether the implementation is S3, Azure, Local, etc.
/// The adapter makes Azure look exactly like any other IFileRepository.
/// </summary>
public class DocumentService
{
    private readonly IFileRepository _repository;

    public DocumentService(IFileRepository repository)
    {
        _repository = repository;
    }

    public void UploadDocument(string fileName, byte[] content)
    {
        Console.WriteLine($"  DocumentService: uploading '{fileName}'");
        _repository.Upload(fileName, content);
    }

    public void DownloadDocument(string fileName)
    {
        Console.WriteLine($"  DocumentService: downloading '{fileName}'");
        var data = _repository.Download(fileName);
        Console.WriteLine($"  DocumentService: received {data.Length} bytes");
    }

    public void DeleteDocument(string fileName)
    {
        if (_repository.Exists(fileName))
        {
            _repository.Delete(fileName);
            Console.WriteLine($"  DocumentService: '{fileName}' deleted successfully");
        }
        else
        {
            Console.WriteLine($"  DocumentService: '{fileName}' not found");
        }
    }
}
