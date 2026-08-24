namespace Command.V2;

public class UploadCommand : ICommand
{
    private readonly FileStorageService _storage;
    private readonly string _fileName;
    private readonly byte[] _content;

    public string Description => $"Upload '{_fileName}' ({_content.Length} bytes)";

    public UploadCommand(FileStorageService storage, string fileName, byte[] content)
    {
        _storage = storage;
        _fileName = fileName;
        _content = content;
    }

    public void Execute() => _storage.Upload(_fileName, _content);
    public void Undo() => _storage.Delete(_fileName); // undo upload = delete
}
