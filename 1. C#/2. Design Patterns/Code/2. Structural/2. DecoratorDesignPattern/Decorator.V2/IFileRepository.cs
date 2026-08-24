namespace Decorator.V2;

/// <summary>
/// The component interface — both the real implementation and all
/// decorators implement this same interface.
/// </summary>
public interface IFileRepository
{
    void Upload(string fileName, byte[] content);
    byte[] Download(string fileName);
    void Delete(string fileName);
}
