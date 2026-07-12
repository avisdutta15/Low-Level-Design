namespace Adapter.V1;

/// <summary>
/// Our application's standard interface for file storage.
/// All services in our app depend on this contract.
/// </summary>
public interface IFileRepository
{
    void Upload(string fileName, byte[] content);
    byte[] Download(string fileName);
    void Delete(string fileName);
    bool Exists(string fileName);
}
