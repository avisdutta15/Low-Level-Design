using FileSystemV3.FileSystem;

namespace FileSystemV3.Shell.Commands;

public class LsCommand : ICommand
{
    public string Name => "ls";

    public string Execute(FileSystemManager fs, string[] args)
    {
        bool detailed = false;
        string? path = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-l")
                detailed = true;
            else
                path = args[i];
        }

        var items = fs.List(path);

        if (!detailed)
        {
            var names = new List<string>();
            foreach (var node in items)
            {
                names.Add(node.Name);
            }
            return string.Join("  ", names);
        }

        var lines = new List<string>();
        foreach (var node in items)
        {
            string type = node is DirectoryNode ? "d" : "-";
            string size = node is FileNode f ? f.Size.ToString() : "-";
            string line = $"{type}  {size,6}  {node.CreatedAt:yyyy-MM-dd HH:mm}  {node.Name}";
            lines.Add(line);
        }
        return string.Join(Environment.NewLine, lines);
    }
}
