using FileSystemV2.FileSystem;

namespace FileSystemV2.Shell.Commands;

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
