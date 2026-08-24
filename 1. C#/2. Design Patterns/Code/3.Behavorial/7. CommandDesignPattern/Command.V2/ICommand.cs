namespace Command.V2;

/// <summary>
/// Command interface — encapsulates an operation as an object.
/// Supports Execute and Undo.
/// </summary>
public interface ICommand
{
    string Description { get; }
    void Execute();
    void Undo();
}
