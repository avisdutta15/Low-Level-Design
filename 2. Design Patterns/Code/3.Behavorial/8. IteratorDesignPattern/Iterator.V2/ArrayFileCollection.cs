namespace Iterator.V2;

/// <summary>
/// Concrete collection backed by an array.
/// Provides iterators — clients never touch the internal array.
/// </summary>
public class ArrayFileCollection : IFileCollection
{
    private readonly FileMetadata[] _files;
    private int _count;

    public int Count => _count;

    public ArrayFileCollection(int capacity)
    {
        _files = new FileMetadata[capacity];
    }

    public void Add(FileMetadata file) => _files[_count++] = file;

    public IFileIterator CreateIterator() => new ArrayFileIterator(_files, _count);

    public IFileIterator CreateFilteredIterator(Func<FileMetadata, bool> predicate)
        => new FilteredFileIterator(new ArrayFileIterator(_files, _count), predicate);

    // --- Concrete iterator (private/internal — hidden from client) ---
    private class ArrayFileIterator : IFileIterator
    {
        private readonly FileMetadata[] _files;
        private readonly int _count;
        private int _position;

        public ArrayFileIterator(FileMetadata[] files, int count)
        {
            _files = files;
            _count = count;
            _position = 0;
        }

        public bool HasNext() => _position < _count;
        public FileMetadata Next() => _files[_position++];
        public void Reset() => _position = 0;
    }
}
