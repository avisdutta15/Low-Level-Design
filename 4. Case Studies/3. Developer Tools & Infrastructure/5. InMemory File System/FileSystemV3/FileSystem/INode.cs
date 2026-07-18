namespace FileSystemV3.FileSystem;

/// <summary>
/// Abstract base class for all file system entries (files and directories).
/// Every node knows its name and its parent directory.
/// </summary>
public abstract class INode
{
    public string Name { get; }
    public DirectoryNode? Parent { get; set; }
    public DateTime CreatedAt { get; }

    protected INode(string name, DirectoryNode? parent)
    {
        Name = name;
        Parent = parent;
        CreatedAt = DateTime.Now;
    }

    /// <summary>
    /// Computes the full absolute path by walking up the parent chain.
    /// Thread-safe: Parent references are set once at construction and never changed.
    /// </summary>
    public string GetFullPath()
    {
        if (Parent == null) return "/";

        var parts = new Stack<string>();
        INode current = this;
        while (current.Parent != null)
        {
            parts.Push(current.Name);
            current = current.Parent;
        }

        return "/" + string.Join("/", parts);
    }
}
