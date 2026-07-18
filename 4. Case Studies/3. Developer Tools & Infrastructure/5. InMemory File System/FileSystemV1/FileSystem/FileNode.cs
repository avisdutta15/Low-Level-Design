namespace FileSystemV1.FileSystem;

/// <summary>
/// Represents a file in the file system.
/// Stores simple string-based content.
/// </summary>
public class FileNode : INode
{
    private string _content;

    public FileNode(string name, DirectoryNode? parent) : base(name, parent)
    {
        _content = string.Empty;
    }

    /// <summary>
    /// Returns the content of this file.
    /// </summary>
    public string Read()
    {
        return _content;
    }

    /// <summary>
    /// Overwrites the content of this file with the given text.
    /// </summary>
    public void Write(string content)
    {
        _content = content;
    }

    /// <summary>
    /// Returns the size (character count) of the file content.
    /// </summary>
    public int Size => _content.Length;
}
