namespace Command.V2;

public class RenameCommand : ICommand
{
    private readonly FileStorageService _storage;
    private readonly string _oldName;
    private readonly string _newName;

    public string Description => $"Rename '{_oldName}' → '{_newName}'";

    public RenameCommand(FileStorageService storage, string oldName, string newName)
    {
        _storage = storage;
        _oldName = oldName;
        _newName = newName;
    }

    public void Execute() => _storage.Rename(_oldName, _newName);
    public void Undo() => _storage.Rename(_newName, _oldName); // reverse the rename
}
