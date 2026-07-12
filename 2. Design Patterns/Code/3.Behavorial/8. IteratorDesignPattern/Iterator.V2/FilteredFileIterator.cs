namespace Iterator.V2;

/// <summary>
/// Decorator iterator: wraps any IFileIterator and filters results.
/// Client uses the same HasNext()/Next() interface.
/// </summary>
public class FilteredFileIterator : IFileIterator
{
    private readonly IFileIterator _inner;
    private readonly Func<FileMetadata, bool> _predicate;
    private FileMetadata? _nextItem;

    public FilteredFileIterator(IFileIterator inner, Func<FileMetadata, bool> predicate)
    {
        _inner = inner;
        _predicate = predicate;
        Advance();
    }

    public bool HasNext() => _nextItem != null;

    public FileMetadata Next()
    {
        var current = _nextItem!;
        Advance();
        return current;
    }

    public void Reset()
    {
        _inner.Reset();
        Advance();
    }

    private void Advance()
    {
        _nextItem = null;
        while (_inner.HasNext())
        {
            var candidate = _inner.Next();
            if (_predicate(candidate))
            {
                _nextItem = candidate;
                return;
            }
        }
    }
}
