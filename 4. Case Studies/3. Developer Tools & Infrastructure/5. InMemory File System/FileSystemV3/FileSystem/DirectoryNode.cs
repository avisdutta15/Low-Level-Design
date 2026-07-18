using System.Collections.Concurrent;

namespace FileSystemV3.FileSystem;

/// <summary>
/// Represents a directory in the file system.
/// Thread-safe: Uses ConcurrentDictionary for child management.
/// 
/// Why ConcurrentDictionary over Copy-on-Write (ImmutableDictionary)?
/// - Directories get files added throughout the session (not just at startup)
/// - ConcurrentDictionary.TryAdd gives atomic check-and-insert (no TOCTOU race)
/// - Good performance for both reads and writes
/// - Copy-on-Write would allocate a new dictionary on every mkdir/touch — overkill
///   unless thousands of threads are calling ls while structure is frozen
/// </summary>
public class DirectoryNode : INode
{
    // ConcurrentDictionary provides:
    // - Thread-safe reads without locking
    // - Atomic TryAdd (check + insert in one operation)
    // - Safe enumeration (snapshot semantics)
    private readonly ConcurrentDictionary<string, INode> _children = new();

    public DirectoryNode(string name, DirectoryNode? parent) : base(name, parent) { }

    /// <summary>
    /// Returns a snapshot of all children for enumeration.
    /// Thread-safe: ConcurrentDictionary.Values returns a moment-in-time snapshot.
    /// </summary>
    public ICollection<INode> Children => _children.Values;

    /// <summary>
    /// Adds a child node (file or directory) to this directory.
    /// Thread-safe: TryAdd is atomic — no race between HasChild and AddChild.
    /// Throws if a child with the same name already exists.
    /// </summary>
    public void AddChild(INode node)
    {
        // TryAdd is atomic: checks existence AND inserts in one operation.
        // This eliminates the TOCTOU (time-of-check-to-time-of-use) race
        // that existed in V1/V2's separate HasChild + AddChild calls.
        if (!_children.TryAdd(node.Name, node))
            throw new InvalidOperationException($"'{node.Name}' already exists in '{Name}'");
    }

    /// <summary>
    /// Returns the child with the given name, or null if not found.
    /// Thread-safe: TryGetValue is lock-free on ConcurrentDictionary.
    /// </summary>
    public INode? GetChild(string name)
    {
        _children.TryGetValue(name, out var node);
        return node;
    }

    /// <summary>
    /// Checks if a child with the given name exists.
    /// Thread-safe: ContainsKey is lock-free on ConcurrentDictionary.
    /// </summary>
    public bool HasChild(string name)
    {
        return _children.ContainsKey(name);
    }
}
