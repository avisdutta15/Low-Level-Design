using FileSystemV2.FileSystem;

namespace FileSystemV2.Shell.Commands;

public class PwdCommand : ICommand
{
    public string Name => "pwd";

    public string Execute(FileSystemManager fs, string[] args)
    {
        return fs.PrintWorkingDirectory();
    }
}
