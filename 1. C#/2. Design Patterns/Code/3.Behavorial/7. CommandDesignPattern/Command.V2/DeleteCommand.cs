namespace Command.V2;

public class DeleteCommand : ICommand
{
    private readonly FileStorageService _storage;
    private readonly string _fileName;
    private byte[]? _backup; // saved for undo

    public string Description => $"Delete '{_fileName}'";

    public DeleteCommand(FileStorageService storage, string fileName)
    {
        _storage = storage;
        _fileName = fileName;
    }

    public void Execute()
    {
        // Save content before deleting (for undo)
        _backup = _storage.Download(_fileName);
        _storage.Delete(_fileName);
    }

    public void Undo()
    {
        // Restore the deleted file
        if (_backup != null)
        {
            _storage.Upload(_fileName, _backup);
            Console.WriteLine($"  [Undo] Restored '{_fileName}'");
        }
    }
}
