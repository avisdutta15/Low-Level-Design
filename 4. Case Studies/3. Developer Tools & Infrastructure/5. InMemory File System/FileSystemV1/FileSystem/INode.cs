namespace FileSystemV1.FileSystem;

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
    /// Example: for a node "notes.txt" inside "/home/user", returns "/home/user/notes.txt"
    /// </summary>
    public string GetFullPath()
    {
        // Root has no parent
        if (Parent == null) return "/";

        // Walk up the tree collecting names
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
