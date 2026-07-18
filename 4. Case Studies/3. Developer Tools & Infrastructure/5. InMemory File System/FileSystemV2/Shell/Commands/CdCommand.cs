using FileSystemV2.FileSystem;

namespace FileSystemV2.Shell.Commands;

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
