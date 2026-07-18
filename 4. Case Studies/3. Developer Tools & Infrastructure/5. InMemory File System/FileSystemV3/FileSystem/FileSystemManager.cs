namespace FileSystemV3.FileSystem;

/// <summary>
/// V3 FileSystemManager — Thread-safe version with absolute + relative paths.
/// 
/// Thread safety approach:
/// - DirectoryNode uses ConcurrentDictionary (atomic TryAdd, lock-free reads)
/// - FileNode uses lock for Read/Write (mutual exclusion + visibility)
/// - CurrentDirectory uses lock for get/set (prevents torn reads during cd)
/// - Navigation is safe because it only reads from ConcurrentDictionary
/// - Creation operations (mkdir, touch) are atomic via TryAdd
/// </summary>
public class FileSystemManager
{
    private readonly DirectoryNode _root;
    private readonly object _currentDirLock = new();
    private DirectoryNode _currentDirectory;

    public FileSystemManager()
    {
        _root = new DirectoryNode("/", null);
        _currentDirectory = _root;
    }

    /// <summary>
    /// Gets the current working directory.
    /// Thread-safe: protected by lock for visibility across threads.
    /// </summary>
    public DirectoryNode CurrentDirectory
    {
        get { lock (_currentDirLock) { return _currentDirectory; } }
        private set { lock (_currentDirLock) { _currentDirectory = value; } }
    }

    /// <summary>
    /// Creates a directory at the given path (absolute or relative).
    /// Thread-safe: uses ConcurrentDictionary.TryAdd internally (atomic).
    /// </summary>
    public void MakeDirectory(string path)
    {
        string parentPath = GetParentPath(path);
        string name = GetLastSegment(path);

        DirectoryNode parent = NavigateToDirectory(parentPath);

        // AddChild uses TryAdd — atomic check + insert
        var dir = new DirectoryNode(name, parent);
        parent.AddChild(dir);
    }

    /// <summary>
    /// Creates a file at the given path (absolute or relative).
    /// Thread-safe: HasChild + AddChild race is acceptable here —
    /// worst case two threads both try to create, one gets the exception
    /// from TryAdd which we catch silently (touch is idempotent).
    /// </summary>
    public void Touch(string path)
    {
        string parentPath = GetParentPath(path);
        string name = GetLastSegment(path);

        DirectoryNode parent = NavigateToDirectory(parentPath);

        // If file already exists, touch is a no-op.
        // Even without this check, TryAdd would throw — but checking
        // first avoids creating a FileNode object unnecessarily.
        if (parent.HasChild(name)) return;

        var file = new FileNode(name, parent);

        // TryAdd might fail if another thread added it between HasChild and here.
        // That's fine — touch on an existing file is a no-op anyway.
        try
        {
            parent.AddChild(file);
        }
        catch (InvalidOperationException)
        {
            // Another thread created it first — that's fine for touch
        }
    }

    /// <summary>
    /// Changes the current working directory to the given path.
    /// Thread-safe: CurrentDirectory setter is locked.
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
    /// Thread-safe: reads CurrentDirectory (locked) then walks immutable parent chain.
    /// </summary>
    public string PrintWorkingDirectory()
    {
        return CurrentDirectory.GetFullPath();
    }

    /// <summary>
    /// Lists entries in a directory. If no path given, lists CurrentDirectory.
    /// Thread-safe: ConcurrentDictionary.Values returns a snapshot.
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

        return dir.Children;
    }

    /// <summary>
    /// Returns the content of the file at the given path.
    /// Thread-safe: FileNode.Read() acquires its internal lock.
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
    /// Thread-safe: FileNode.Write() acquires its internal lock.
    /// </summary>
    public void WriteFile(string path, string content)
    {
        INode node = Navigate(path);

        if (node is not FileNode file)
            throw new InvalidOperationException($"echo: '{path}' is not a file");

        file.Write(content);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Path Resolution
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates to a node at the given path.
    /// Thread-safe: only reads from ConcurrentDictionary (lock-free reads).
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

        // Decide starting point
        DirectoryNode current;
        if (path.StartsWith("/"))
            current = _root;
        else
            current = CurrentDirectory;

        // Traverse each segment
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];

            // "." means current directory — skip
            if (part == "." || part == "")
                continue;

            // ".." means go up to parent
            if (part == "..")
            {
                if (current.Parent != null)
                    current = current.Parent;
                continue;
            }

            // Look up child — ConcurrentDictionary.TryGetValue is lock-free
            INode? child = current.GetChild(part);
            if (child == null)
                throw new InvalidOperationException($"Path not found: '{path}'");

            // Last segment can be a file
            if (i == parts.Length - 1)
                return child;

            // Intermediate segments must be directories
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
        if (string.IsNullOrEmpty(path) || path == "/")
            return path == "/" ? _root : CurrentDirectory;

        if (path == ".")
            return CurrentDirectory;

        INode node = Navigate(path);

        if (node is not DirectoryNode dir)
            throw new InvalidOperationException($"'{path}' is not a directory");

        return dir;
    }

    // ─────────────────────────────────────────────────────────────────────
    // String helpers
    // ─────────────────────────────────────────────────────────────────────

    private string[] SplitPath(string path)
    {
        return path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private string GetParentPath(string path)
    {
        int lastSlash = path.LastIndexOf('/');

        if (lastSlash < 0)
            return ".";

        if (lastSlash == 0)
            return "/";

        return path.Substring(0, lastSlash);
    }

    private string GetLastSegment(string path)
    {
        int lastSlash = path.LastIndexOf('/');

        if (lastSlash < 0)
            return path;

        return path.Substring(lastSlash + 1);
    }
}
