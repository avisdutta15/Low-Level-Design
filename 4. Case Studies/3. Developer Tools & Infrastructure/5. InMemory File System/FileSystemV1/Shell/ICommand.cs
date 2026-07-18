using FileSystemV1.FileSystem;

namespace FileSystemV1.Shell;

/// <summary>
/// Interface for all shell commands.
/// Each command has a name (used to match user input) and an Execute method.
/// </summary>
public interface ICommand
{
    string Name { get; }
    string Execute(FileSystemManager fs, string[] args);
}
