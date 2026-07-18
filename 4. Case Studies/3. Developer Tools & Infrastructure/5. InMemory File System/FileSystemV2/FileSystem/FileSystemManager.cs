namespace FileSystemV2.FileSystem;

/// <summary>
/// V2 FileSystemManager — supports BOTH absolute and relative paths.
/// 
/// Key differences from V1:
/// - Maintains a CurrentDirectory (working directory)
/// - Supports "cd" to change directories
/// - Supports ".." (parent) and "." (current) in paths
/// - Paths starting with "/" resolve from root (absolute)
/// - Paths without leading "/" resolve from CurrentDirectory (relative)
/// </summary>
public class FileSystemManager
{
    private readonly DirectoryNode _root;
    public DirectoryNode CurrentDirectory { get; private set; }

    public FileSystemManager()
    {
        _root = new DirectoryNode("/", null);
        CurrentDirectory = _root;
    }

    /// <summary>
    /// Creates a directory at the given path (absolute or relative).
    /// Example: MakeDirectory("docs") creates "docs" inside CurrentDirectory.
    /// Example: MakeDirectory("/home/user") creates "user" inside "/home".
    /// </summary>
    public void MakeDirectory(string path)
    {
        // Split into parent path + new name
        string parentPath = GetParentPath(path);
        string name = GetLastSegment(path);

        // Navigate to parent (handles both absolute and relative)
        DirectoryNode parent = NavigateToDirectory(parentPath);

        if (parent.HasChild(name))
            throw new InvalidOperationException($"mkdir: '{name}' already exists");

        var dir = new DirectoryNode(name, parent);
        parent.AddChild(dir);
    }

    /// <summary>
    /// Creates a file at the given path (absolute or relative).
    /// If the file already exists, this is a no-op.
    /// </summary>
    public void Touch(string path)
    {
        string parentPath = GetParentPath(path);
        string name = GetLastSegment(path);

        DirectoryNode parent = NavigateToDirectory(parentPath);

        // Touch on existing file is a no-op
        if (parent.HasChild(name)) return;

        var file = new FileNode(name, parent);
        parent.AddChild(file);
    }

    /// <summary>
    /// Changes the current working directory to the given path.
    /// Supports absolute paths ("/home/user"), relative paths ("docs"),
    /// and parent traversal ("..").
    /// </summary>
    public void ChangeDirectory(string path)
    {
        INode target = Navigate(path);

        if (target is not DirectoryNode dir)
            throw new InvalidOperationException($"cd: '{path}' is not a directory");

        CurrentDirectory = dir;
    }

    /// <summary>
    /// Returns the full absolute path of the current working directory.
    /// </summary>
    public string PrintWorkingDirectory()
    {
        return CurrentDirectory.GetFullPath();
    }

    /// <summary>
    /// Lists entries in a directory. If no path given, lists CurrentDirectory.
    /// </summary>
    public IEnumerable<INode> List(string? path = null)
    {
        DirectoryNode dir;

        if (path == null)
        {
            dir = CurrentDirectory;
        }
        else
        {
            INode node = Navigate(path);
            if (node is not DirectoryNode d)
                throw new InvalidOperationException($"ls: '{path}' is not a directory");
            dir = d;
        }

        return dir.Children.Values;
    }

    /// <summary>
    /// Returns the content of the file at the given path.
    /// </summary>
    public string Cat(string path)
    {
        INode node = Navigate(path);

        if (node is not FileNode file)
            throw new InvalidOperationException($"cat: '{path}' is not a file");

        return file.Read();
    }

    /// <summary>
    /// Writes content to the file at the given path.
    /// </summary>
    public void WriteFile(string path, string content)
    {
        INode node = Navigate(path);

        if (node is not FileNode file)
            throw new InvalidOperationException($"echo: '{path}' is not a file");

        file.Write(content);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Path Resolution (the key difference from V1)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates to a node at the given path.
    /// 
    /// Path resolution rules:
    /// - Starts with "/" → absolute, resolve from root
    /// - Does NOT start with "/" → relative, resolve from CurrentDirectory
    /// - ".." → go to parent directory
    /// - "." → stay in current (skip)
    /// </summary>
    public INode Navigate(string path)
    {
        string[] parts = SplitPath(path);

        // Decide starting point: absolute (from root) vs relative (from current dir)
        DirectoryNode current;
        if (path.StartsWith("/"))
            current = _root;
        else
            current = CurrentDirectory;

        // Traverse each segment of the path
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];

            // "." means current directory — skip it
            if (part == "." || part == "")
                continue;

            // ".." means go up to parent
            if (part == "..")
            {
                // If already at root, stay at root (can't go above root)
                if (current.Parent != null)
                    current = current.Parent;
                continue;
            }

            // Look up the child by name
            INode? child = current.GetChild(part);
            if (child == null)
                throw new InvalidOperationException($"Path not found: '{path}'");

            // If it's the last segment, it could be a file — return it directly
            if (i == parts.Length - 1)
                return child;

            // Otherwise, it must be a directory (intermediate segments must be dirs)
            if (child is not DirectoryNode dir)
                throw new InvalidOperationException($"'{part}' is not a directory in path '{path}'");

            current = dir;
        }

        return current;
    }

    /// <summary>
    /// Navigates to a directory at the given path.
    /// Throws if the result is not a directory.
    /// </summary>
    private DirectoryNode NavigateToDirectory(string path)
    {
        // Handle empty or root path
        if (string.IsNullOrEmpty(path) || path == "/")
            return path == "/" ? _root : CurrentDirectory;

        // Special case: "." means current directory
        if (path == ".")
            return CurrentDirectory;

        INode node = Navigate(path);

        if (node is not DirectoryNode dir)
            throw new InvalidOperationException($"'{path}' is not a directory");

        return dir;
    }

    // ─────────────────────────────────────────────────────────────────────
    // String helpers (simple, no modern syntax)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Splits a path into segments.
    /// "/home/user/file.txt" → ["home", "user", "file.txt"]
    /// "docs/notes" → ["docs", "notes"]
    /// </summary>
    private string[] SplitPath(string path)
    {
        return path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Gets the parent portion of a path.
    /// 
    /// For absolute paths:
    ///   "/home/user/notes.txt" → "/home/user"
    ///   "/home" → "/"
    /// 
    /// For relative paths:
    ///   "docs/notes.txt" → "docs"
    ///   "notes.txt" → "." (current directory)
    /// </summary>
    private string GetParentPath(string path)
    {
        int lastSlash = path.LastIndexOf('/');

        // No slash found — it's a simple name like "notes.txt"
        // Parent is the current directory
        if (lastSlash < 0)
            return ".";

        // Slash at position 0 — parent is root "/"
        if (lastSlash == 0)
            return "/";

        // Return everything before the last slash
        return path.Substring(0, lastSlash);
    }

    /// <summary>
    /// Gets the last segment (file or directory name) from a path.
    /// "/home/user/notes.txt" → "notes.txt"
    /// "docs/file.txt" → "file.txt"
    /// "notes.txt" → "notes.txt"
    /// </summary>
    private string GetLastSegment(string path)
    {
        int lastSlash = path.LastIndexOf('/');

        // No slash — the entire string is the name
        if (lastSlash < 0)
            return path;

        // Return everything after the last slash
        return path.Substring(lastSlash + 1);
    }
}
