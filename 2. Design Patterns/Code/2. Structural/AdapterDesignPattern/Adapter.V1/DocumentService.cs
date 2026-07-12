namespace Adapter.V1;

/// <summary>
/// Our application service — depends on IFileRepository.
/// Works perfectly with S3FileRepository.
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
        _repository.Download(fileName);
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
