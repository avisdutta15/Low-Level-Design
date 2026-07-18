using FileSystemV1.FileSystem;
using FileSystemV1.Shell;
using FileSystemV1.Shell.Commands;

// ─────────────────────────────────────────────────────────────────────
// V1: In-Memory File System with ABSOLUTE paths only.
// All paths must start with "/".
// No current working directory, no cd, no ".." traversal.
// ─────────────────────────────────────────────────────────────────────

var fs = new FileSystemManager();
var shell = new Shell(fs);

// Register all supported commands
shell.RegisterCommand(new MkdirCommand());
shell.RegisterCommand(new TouchCommand());
shell.RegisterCommand(new LsCommand());
shell.RegisterCommand(new CatCommand());
shell.RegisterCommand(new EchoCommand());

// ─────────────────────────────────────────────────────────────────────
// Demo walkthrough
// ─────────────────────────────────────────────────────────────────────
Console.WriteLine("=== In-Memory File System V1 (Absolute Paths Only) ===\n");

RunCommand("mkdir /home");
RunCommand("mkdir /home/user");
RunCommand("mkdir /home/user/docs");
RunCommand("touch /home/user/notes.txt");
RunCommand("echo \"hello world\" > /home/user/notes.txt");
RunCommand("cat /home/user/notes.txt");
RunCommand("ls /");
RunCommand("ls /home/user");
RunCommand("ls -l /home/user");

// Error cases
Console.WriteLine("\n--- Error cases ---");
RunCommand("mkdir /home");              // already exists
RunCommand("cat /home/user/docs");      // not a file
RunCommand("cat /nonexistent/file.txt"); // path not found

Console.WriteLine("\n=== Interactive Shell ===\n");

// Interactive REPL
while (true)
{
    Console.Write("$ ");
    var input = Console.ReadLine();
    if (input == null || input.Trim() == "exit") break;

    var output = shell.Execute(input);
    if (!string.IsNullOrEmpty(output))
        Console.WriteLine(output);
}

void RunCommand(string input)
{
    Console.WriteLine($"$ {input}");
    var output = shell.Execute(input);
    if (!string.IsNullOrEmpty(output))
        Console.WriteLine(output);
}
