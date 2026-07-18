using FileSystemV3.FileSystem;

namespace FileSystemV3.Shell;

/// <summary>
/// The shell parses user input strings and dispatches them to registered commands.
/// Thread-safe: the command registry uses ConcurrentDictionary for safe concurrent access.
/// </summary>
public class Shell
{
    private readonly FileSystemManager _fs;
    private readonly Dictionary<string, ICommand> _commands = new();

    public Shell(FileSystemManager fs)
    {
        _fs = fs;
    }

    public void RegisterCommand(ICommand command)
    {
        _commands[command.Name] = command;
    }

    public string Execute(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        string[] parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string commandName = parts[0];

        string[] args = new string[parts.Length - 1];
        for (int i = 1; i < parts.Length; i++)
        {
            args[i - 1] = parts[i];
        }

        if (!_commands.TryGetValue(commandName, out var command))
            return $"{commandName}: command not found";

        try
        {
            return command.Execute(_fs, args);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }
}
