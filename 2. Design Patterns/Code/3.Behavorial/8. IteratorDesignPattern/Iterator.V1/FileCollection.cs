namespace Iterator.V1;

/// <summary>
/// Without Iterator: Internal data structure is exposed directly.
/// Clients must know the underlying implementation (array, list, tree, etc.)
/// to traverse files. If the structure changes, all clients break.
/// </summary>
public class FileMetadata
{
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string Author { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public class FileCollectionAsArray
{
    // Internal storage: array (fixed-size)
    private readonly FileMetadata[] _files;
    private int _count;

    public FileCollectionAsArray(int capacity)
    {
        _files = new FileMetadata[capacity];
        _count = 0;
    }

    public void Add(FileMetadata file) => _files[_count++] = file;

    // EXPOSES internal structure — client must use index-based loop
    public int Count => _count;
    public FileMetadata GetAt(int index) => _files[index];
}

public class FileCollectionAsList
{
    // Internal storage: List (different API than array!)
    private readonly List<FileMetadata> _files = new();

    public void Add(FileMetadata file) => _files.Add(file);

    // EXPOSES internal structure — client uses List API
    public List<FileMetadata> GetAll() => _files;
}

public class FileCollectionAsLinkedList
{
    // Internal storage: linked list node structure
    public class Node
    {
        public FileMetadata Data { get; }
        public Node? Next { get; set; }
        public Node(FileMetadata data) => Data = data;
    }

    private Node? _head;

    public void Add(FileMetadata file)
    {
        var node = new Node(file) { Next = _head };
        _head = node;
    }

    // EXPOSES internal structure — client must follow Node.Next pointers
    public Node? Head => _head;
}
