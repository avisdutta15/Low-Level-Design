using FileSystemV2.FileSystem;

namespace FileSystemV2.Shell;

/// <summary>
/// Interface for all shell commands.
/// Each command has a name (used to match user input) and an Execute method.
/// </summary>
public interface ICommand
{
    string Name { get; }
    string Execute(FileSystemManager fs, string[] args);
}
