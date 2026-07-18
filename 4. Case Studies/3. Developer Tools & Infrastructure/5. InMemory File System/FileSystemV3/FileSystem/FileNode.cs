namespace FileSystemV3.FileSystem;

/// <summary>
/// Represents a file in the file system.
/// Thread-safe: Read() and Write() are protected by a lock to ensure
/// mutual exclusion and memory visibility across threads.
/// </summary>
public class FileNode : INode
{
    private readonly object _lock = new();
    private string _content;

    public FileNode(string name, DirectoryNode? parent) : base(name, parent)
    {
        _content = string.Empty;
    }

    /// <summary>
    /// Returns the content of this file.
    /// Thread-safe: acquires lock to ensure visibility of latest write.
    /// </summary>
    public string Read()
    {
        lock (_lock)
        {
            return _content;
        }
    }

    /// <summary>
    /// Overwrites the content of this file with the given text.
    /// Thread-safe: acquires lock to ensure atomicity and visibility.
    /// </summary>
    public void Write(string content)
    {
        lock (_lock)
        {
            _content = content;
        }
    }

    /// <summary>
    /// Returns the size (character count) of the file content.
    /// Thread-safe: acquires lock to read consistent state.
    /// </summary>
    public int Size
    {
        get
        {
            lock (_lock)
            {
                return _content.Length;
            }
        }
    }
}
