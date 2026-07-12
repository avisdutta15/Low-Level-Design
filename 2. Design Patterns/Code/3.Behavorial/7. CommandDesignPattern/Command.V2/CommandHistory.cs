namespace Command.V2;

/// <summary>
/// Invoker — executes commands and maintains history for undo/redo.
/// Doesn't know what the commands do — just invokes them.
/// </summary>
public class CommandHistory
{
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();

    public void Execute(ICommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear(); // new action invalidates redo history
        Console.WriteLine($"  [History] Executed: {command.Description}");
    }

    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            Console.WriteLine("  [History] Nothing to undo");
            return;
        }

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        Console.WriteLine($"  [History] Undone: {command.Description}");
    }

    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            Console.WriteLine("  [History] Nothing to redo");
            return;
        }

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        Console.WriteLine($"  [History] Redone: {command.Description}");
    }

    public void PrintHistory()
    {
        Console.WriteLine($"  [History] Undo stack ({_undoStack.Count}):");
        foreach (var cmd in _undoStack)
            Console.WriteLine($"      {cmd.Description}");
    }
}
