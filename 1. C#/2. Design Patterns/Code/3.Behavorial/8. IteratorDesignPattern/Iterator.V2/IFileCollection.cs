namespace Iterator.V2;

/// <summary>
/// Aggregate interface — any collection that can produce an iterator.
/// Client only needs this — never accesses the internal structure directly.
/// </summary>
public interface IFileCollection
{
    IFileIterator CreateIterator();
    IFileIterator CreateFilteredIterator(Func<FileMetadata, bool> predicate);
    void Add(FileMetadata file);
    int Count { get; }
}
