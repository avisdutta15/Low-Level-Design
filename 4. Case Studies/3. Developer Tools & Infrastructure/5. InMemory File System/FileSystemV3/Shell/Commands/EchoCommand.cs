using FileSystemV3.FileSystem;

namespace FileSystemV3.Shell.Commands;

public class EchoCommand : ICommand
{
    public string Name => "echo";

    public string Execute(FileSystemManager fs, string[] args)
    {
        // Find the ">" redirect operator
        int redirectIndex = -1;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == ">")
            {
                redirectIndex = i;
                break;
            }
        }

        // No redirect — just echo the text back
        if (redirectIndex < 0 || redirectIndex >= args.Length - 1)
            return string.Join(" ", args);

        // Collect content (everything before ">") and trim quotes
        string content = "";
        for (int i = 0; i < redirectIndex; i++)
        {
            if (i > 0) content += " ";
            content += args[i];
        }
        content = content.Trim('"');

        // The file path is after ">"
        string path = args[redirectIndex + 1];

        // Create the file if it doesn't exist, then write content
        try { fs.Cat(path); }
        catch { fs.Touch(path); }

        fs.WriteFile(path, content);
        return string.Empty;
    }
}
