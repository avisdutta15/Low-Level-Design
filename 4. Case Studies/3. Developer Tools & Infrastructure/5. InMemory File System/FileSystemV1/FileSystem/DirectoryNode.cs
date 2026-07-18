namespace FileSystemV1.FileSystem;

/// <summary>
/// Represents a directory in the file system.
/// Maintains a dictionary of children for O(1) lookup by name.
/// </summary>
public class DirectoryNode : INode
{
    // Dictionary gives us O(1) child lookup by name
    private readonly Dictionary<string, INode> _children = new();

    public DirectoryNode(string name, DirectoryNode? parent) : base(name, parent) { }

    public IReadOnlyDictionary<string, INode> Children => _children;

    /// <summary>
    /// Adds a child node (file or directory) to this directory.
    /// Throws if a child with the same name already exists.
    /// </summary>
    public void AddChild(INode node)
    {
        if (_children.ContainsKey(node.Name))
            throw new InvalidOperationException($"'{node.Name}' already exists in '{Name}'");

        _children[node.Name] = node;
    }

    /// <summary>
    /// Returns the child with the given name, or null if not found.
    /// </summary>
    public INode? GetChild(string name)
    {
        _children.TryGetValue(name, out var node);
        return node;
    }

    /// <summary>
    /// Checks if a child with the given name exists.
    /// </summary>
    public bool HasChild(string name)
    {
        return _children.ContainsKey(name);
    }
}
