using FileSystemV3.FileSystem;

namespace FileSystemV3.Shell.Commands;

public class MkdirCommand : ICommand
{
    public string Name => "mkdir";

    public string Execute(FileSystemManager fs, string[] args)
    {
        if (args.Length == 0) return "mkdir: missing operand";
        fs.MakeDirectory(args[0]);
        return string.Empty;
    }
}
