namespace Iterator.V2;

/// <summary>
/// Concrete collection backed by a List&lt;T&gt;.
/// Same IFileIterator interface — client code doesn't change.
/// </summary>
public class ListFileCollection : IFileCollection
{
    private readonly List<FileMetadata> _files = new();

    public int Count => _files.Count;

    public void Add(FileMetadata file) => _files.Add(file);

    public IFileIterator CreateIterator() => new ListFileIterator(_files);

    public IFileIterator CreateFilteredIterator(Func<FileMetadata, bool> predicate)
        => new FilteredFileIterator(new ListFileIterator(_files), predicate);

    private class ListFileIterator : IFileIterator
    {
        private readonly List<FileMetadata> _files;
        private int _position;

        public ListFileIterator(List<FileMetadata> files)
        {
            _files = files;
            _position = 0;
        }

        public bool HasNext() => _position < _files.Count;
        public FileMetadata Next() => _files[_position++];
        public void Reset() => _position = 0;
    }
}
