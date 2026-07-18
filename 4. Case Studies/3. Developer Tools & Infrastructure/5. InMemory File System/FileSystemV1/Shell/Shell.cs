using FileSystemV1.FileSystem;

namespace FileSystemV1.Shell;

/// <summary>
/// The shell parses user input strings and dispatches them to registered commands.
/// Commands are looked up by name from a dictionary (registry pattern).
/// </summary>
public class Shell
{
    private readonly FileSystemManager _fs;
    private readonly Dictionary<string, ICommand> _commands = new();

    public Shell(FileSystemManager fs)
    {
        _fs = fs;
    }

    /// <summary>
    /// Register a command so it can be invoked by name.
    /// </summary>
    public void RegisterCommand(ICommand command)
    {
        _commands[command.Name] = command;
    }

    /// <summary>
    /// Parse and execute a user input string.
    /// Returns the output text, or an error message.
    /// </summary>
    public string Execute(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Split input into command name and arguments
        string[] parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string commandName = parts[0];

        // Extract arguments (everything after the command name)
        string[] args = new string[parts.Length - 1];
        for (int i = 1; i < parts.Length; i++)
        {
            args[i - 1] = parts[i];
        }

        // Look up the command
        if (!_commands.TryGetValue(commandName, out var command))
            return $"{commandName}: command not found";

        // Execute with error handling
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
