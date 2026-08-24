namespace Command.V1;

/// <summary>
/// Without Command Pattern: Operations are called directly with no way to
/// undo, queue, log, or replay them. The invoker (client) is tightly
/// coupled to the receiver (storage service).
/// </summary>
public class FileStorageService
{
    private readonly Dictionary<string, byte[]> _files = new();

    public void Upload(string fileName, byte[] content)
    {
        _files[fileName] = content;
        Console.WriteLine($"  [Storage] Uploaded '{fileName}' ({content.Length} bytes)");
    }

    public byte[] Download(string fileName)
    {
        Console.WriteLine($"  [Storage] Downloaded '{fileName}'");
        return _files.GetValueOrDefault(fileName, Array.Empty<byte>());
    }

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

    public void ListFiles()
    {
        Console.WriteLine($"  [Storage] Files: [{string.Join(", ", _files.Keys)}]");
    }
}
