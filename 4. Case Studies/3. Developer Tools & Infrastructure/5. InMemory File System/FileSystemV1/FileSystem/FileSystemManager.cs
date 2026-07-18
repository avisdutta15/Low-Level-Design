namespace FileSystemV1.FileSystem;

/// <summary>
/// V1 FileSystemManager — supports ABSOLUTE paths only.
/// All paths must start with "/" and are resolved from root.
/// No current working directory, no relative paths, no ".." traversal.
/// </summary>
public class FileSystemManager
{
    private readonly DirectoryNode _root;

    public FileSystemManager()
    {
        _root = new DirectoryNode("/", null);
    }

    /// <summary>
    /// Creates a directory at the given absolute path.
    /// Example: MakeDirectory("/home/user") creates "user" inside "/home".
    /// The parent path must already exist.
    /// </summary>
    public void MakeDirectory(string path)
    {
        // Split path into parent directory and new directory name
        string parentPath = GetParentPath(path);
        string newDirectory = GetLastSegment(path);

        // Navigate to the parent directory
        DirectoryNode parent = NavigateToDirectory(parentPath);

        // Check if already exists
        if (parent.HasChild(newDirectory))
            throw new InvalidOperationException($"mkdir: '{newDirectory}' already exists");

        // Create and add the new directory
        var dir = new DirectoryNode(newDirectory, parent);
        parent.AddChild(dir);
    }

    /// <summary>
    /// Creates a file at the given absolute path.
    /// Example: Touch("/home/user/notes.txt") creates "notes.txt" inside "/home/user".
    /// </summary>
    public void Touch(string path)
    {
        string parentPath = GetParentPath(path);
        string name = GetLastSegment(path);

        DirectoryNode parent = NavigateToDirectory(parentPath);

        // If file already exists, touch is a no-op
        if (parent.HasChild(name)) return;

        var file = new FileNode(name, parent);
        parent.AddChild(file);
    }

    /// <summary>
    /// Lists all entries in the directory at the given absolute path.
    /// If path is "/" or empty, lists the root directory.
    /// </summary>
    public IEnumerable<INode> List(string path)
    {
        DirectoryNode dir = NavigateToDirectory(path);
        return dir.Children.Values;
    }

    /// <summary>
    /// Reads and returns the content of the file at the given absolute path.
    /// </summary>
    public string Cat(string path)
    {
        INode node = Navigate(path);

        if (node is not FileNode file)
            throw new InvalidOperationException($"cat: '{path}' is not a file");

        return file.Read();
    }

    /// <summary>
    /// Writes content to the file at the given absolute path.
    /// The file must already exist.
    /// </summary>
    public void WriteFile(string path, string content)
    {
        INode node = Navigate(path);

        if (node is not FileNode file)
            throw new InvalidOperationException($"echo: '{path}' is not a file");

        file.Write(content);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates to a node at the given absolute path.
    /// Can return either a FileNode or DirectoryNode.
    /// </summary>
    private INode Navigate(string path)
    {
        // Split the path into segments: "/home/user/file.txt" → ["home", "user", "file.txt"]
        string[] parts = SplitPath(path);

        // Start from root
        DirectoryNode current = _root;

        // Traverse all segments except the last one (those must be directories)
        for (int i = 0; i < parts.Length - 1; i++)
        {
            INode? child = current.GetChild(parts[i]);

            if (child == null)
                throw new InvalidOperationException($"Path not found: '{path}'");

            if (child is not DirectoryNode dir)
                throw new InvalidOperationException($"'{parts[i]}' is not a directory in path '{path}'");

            current = dir;
        }

        // Handle the last segment — could be a file or directory
        if (parts.Length == 0)
            return _root;

        string lastName = parts[parts.Length - 1];
        INode? target = current.GetChild(lastName);

        if (target == null)
            throw new InvalidOperationException($"Path not found: '{path}'");

        return target;
    }

    /// <summary>
    /// Navigates to a directory at the given absolute path.
    /// Throws if the target is not a directory.
    /// </summary>
    private DirectoryNode NavigateToDirectory(string path)
    {
        // Handle root path
        if (string.IsNullOrEmpty(path) || path == "/")
            return _root;

        INode node = Navigate(path);

        if (node is not DirectoryNode dir)
            throw new InvalidOperationException($"'{path}' is not a directory");

        return dir;
    }

    /// <summary>
    /// Splits an absolute path into segments.
    /// "/home/user/file.txt" → ["home", "user", "file.txt"]
    /// </summary>
    private string[] SplitPath(string path)
    {
        // Remove leading slash and split by "/"
        // Filter out empty entries (handles double slashes like "/home//user")
        return path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Gets the parent directory path from a full path.
    /// "/home/user/notes.txt" → "/home/user"
    /// "/home" → "/"
    /// </summary>
    private string GetParentPath(string path)
    {
        int lastSlash = path.LastIndexOf('/');

        // If the last slash is at position 0, parent is root "/"
        if (lastSlash <= 0)
            return "/";

        // Return everything before the last slash
        return path.Substring(0, lastSlash);
    }

    /// <summary>
    /// Gets the last segment (file or directory name) from a path.
    /// "/home/user/notes.txt" → "notes.txt"
    /// "/home" → "home"
    /// </summary>
    private string GetLastSegment(string path)
    {
        int lastSlash = path.LastIndexOf('/');

        // Return everything after the last slash
        return path.Substring(lastSlash + 1);
    }
}
