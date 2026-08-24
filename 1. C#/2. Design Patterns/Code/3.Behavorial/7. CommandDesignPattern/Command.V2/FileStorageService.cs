namespace Command.V2;

/// <summary>
/// Receiver — the actual service that performs the work.
/// Doesn't know about commands — just does its job.
/// </summary>
public class FileStorageService
{
    private readonly Dictionary<string, byte[]> _files = new();

    public void Upload(string fileName, byte[] content)
    {
        _files[fileName] = content;
        Console.WriteLine($"  [Storage] Uploaded '{fileName}' ({content.Length} bytes)");
    }

    public byte[]? Download(string fileName)
        => _files.GetValueOrDefault(fileName);

    public void Delete(string fileName)
    {
        _files.Remove(fileName);
        Console.WriteLine($"  [Storage] Deleted '{fileName}'");
    }

    public void Rename(string oldName, string newName)
    {
        if (_files.TryGetValue(oldName, out var content))
        {
            _files.Remove(oldName);
            _files[newName] = content;
            Console.WriteLine($"  [Storage] Renamed '{oldName}' → '{newName}'");
        }
    }

    public bool Exists(string fileName) => _files.ContainsKey(fileName);

    public void ListFiles()
        => Console.WriteLine($"  [Storage] Files: [{string.Join(", ", _files.Keys)}]");
}
