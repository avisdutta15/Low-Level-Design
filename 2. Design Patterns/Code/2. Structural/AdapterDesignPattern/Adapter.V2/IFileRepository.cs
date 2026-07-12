namespace Adapter.V2;

/// <summary>
/// Target interface — what our application expects.
/// All services depend on this contract. We can't change it.
/// </summary>
public interface IFileRepository
{
    void Upload(string fileName, byte[] content);
    byte[] Download(string fileName);
    void Delete(string fileName);
    bool Exists(string fileName);
}
