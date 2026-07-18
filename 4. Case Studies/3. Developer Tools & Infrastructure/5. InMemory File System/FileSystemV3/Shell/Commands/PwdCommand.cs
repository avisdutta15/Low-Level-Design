using FileSystemV3.FileSystem;

namespace FileSystemV3.Shell.Commands;

public class PwdCommand : ICommand
{
    public string Name => "pwd";

    public string Execute(FileSystemManager fs, string[] args)
    {
        return fs.PrintWorkingDirectory();
    }
}
