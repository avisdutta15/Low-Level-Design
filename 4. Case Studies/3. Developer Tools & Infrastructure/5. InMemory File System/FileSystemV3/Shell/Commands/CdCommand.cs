using FileSystemV3.FileSystem;

namespace FileSystemV3.Shell.Commands;

public class CdCommand : ICommand
{
    public string Name => "cd";

    public string Execute(FileSystemManager fs, string[] args)
    {
        if (args.Length == 0) return "cd: missing operand";
        fs.ChangeDirectory(args[0]);
        return string.Empty;
    }
}
