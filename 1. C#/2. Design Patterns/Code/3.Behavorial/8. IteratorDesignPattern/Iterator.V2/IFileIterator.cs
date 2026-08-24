namespace Iterator.V2;

/// <summary>
/// Iterator interface — provides a uniform way to traverse
/// any collection regardless of its internal structure.
/// </summary>
public interface IFileIterator
{
    bool HasNext();
    FileMetadata Next();
    void Reset();
}
