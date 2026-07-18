using FileSystemV1.FileSystem;

namespace FileSystemV1.Shell.Commands;

public class CatCommand : ICommand
{
    public string Name => "cat";

    public string Execute(FileSystemManager fs, string[] args)
    {
        if (args.Length == 0) return "cat: missing operand";
        return fs.Cat(args[0]);
    }
}
