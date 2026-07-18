using FileSystemV3.FileSystem;

namespace FileSystemV3.Shell.Commands;

public class TouchCommand : ICommand
{
    public string Name => "touch";

    public string Execute(FileSystemManager fs, string[] args)
    {
        if (args.Length == 0) return "touch: missing operand";
        fs.Touch(args[0]);
        return string.Empty;
    }
}
