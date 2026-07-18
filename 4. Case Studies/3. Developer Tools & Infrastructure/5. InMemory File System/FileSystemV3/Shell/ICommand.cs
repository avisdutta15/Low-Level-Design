using FileSystemV3.FileSystem;

namespace FileSystemV3.Shell;

/// <summary>
/// Interface for all shell commands.
/// </summary>
public interface ICommand
{
    string Name { get; }
    string Execute(FileSystemManager fs, string[] args);
}
