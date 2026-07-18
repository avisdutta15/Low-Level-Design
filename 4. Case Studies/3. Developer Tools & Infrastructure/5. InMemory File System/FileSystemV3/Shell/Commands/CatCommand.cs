using FileSystemV3.FileSystem;

namespace FileSystemV3.Shell.Commands;

public class CatCommand : ICommand
{
    public string Name => "cat";

    public string Execute(FileSystemManager fs, string[] args)
    {
        if (args.Length == 0) return "cat: missing operand";
        return fs.Cat(args[0]);
    }
}
