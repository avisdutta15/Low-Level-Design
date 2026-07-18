using FileSystemV2.FileSystem;
using FileSystemV2.Shell;
using FileSystemV2.Shell.Commands;

// ─────────────────────────────────────────────────────────────────────
// V2: In-Memory File System with ABSOLUTE + RELATIVE paths.
// Supports cd, pwd, ".." (parent), "." (current).
// Paths with "/" are absolute, paths without are relative.
// ─────────────────────────────────────────────────────────────────────

var fs = new FileSystemManager();
var shell = new Shell(fs);

// Register all supported commands
shell.RegisterCommand(new MkdirCommand());
shell.RegisterCommand(new CdCommand());
shell.RegisterCommand(new TouchCommand());
shell.RegisterCommand(new LsCommand());
shell.RegisterCommand(new PwdCommand());
shell.RegisterCommand(new CatCommand());
shell.RegisterCommand(new EchoCommand());

// ─────────────────────────────────────────────────────────────────────
// Demo walkthrough
// ─────────────────────────────────────────────────────────────────────
Console.WriteLine("=== In-Memory File System V2 (Absolute + Relative Paths) ===\n");

// Absolute path operations (same as V1)
Console.WriteLine("--- Absolute paths (same as V1) ---");
RunCommand("mkdir /home");
RunCommand("mkdir /home/user");
RunCommand("pwd");

// Relative path operations (new in V2)
Console.WriteLine("\n--- Relative paths (new in V2) ---");
RunCommand("cd /home/user");
RunCommand("pwd");
RunCommand("mkdir docs");           // relative: creates /home/user/docs
RunCommand("touch notes.txt");      // relative: creates /home/user/notes.txt
RunCommand("echo \"hello world\" > notes.txt");
RunCommand("cat notes.txt");
RunCommand("ls");
RunCommand("ls -l");

// Parent traversal with ".."
Console.WriteLine("\n--- Parent traversal with \"..\" ---");
RunCommand("cd ..");
RunCommand("pwd");
RunCommand("cd ..");
RunCommand("pwd");
RunCommand("ls");

// Mixed: relative path with ".."
Console.WriteLine("\n--- Mixed relative path ---");
RunCommand("cd /home/user");
RunCommand("cat ../user/notes.txt"); // go up to /home, then back into user

// Error cases
Console.WriteLine("\n--- Error cases ---");
RunCommand("cd notes.txt");          // not a directory
RunCommand("cat docs");              // not a file
RunCommand("cd nonexistent");        // path not found

Console.WriteLine("\n=== Interactive Shell ===\n");

// Interactive REPL
while (true)
{
    Console.Write($"{fs.PrintWorkingDirectory()} $ ");
    var input = Console.ReadLine();
    if (input == null || input.Trim() == "exit") break;

    var output = shell.Execute(input);
    if (!string.IsNullOrEmpty(output))
        Console.WriteLine(output);
}

void RunCommand(string input)
{
    Console.WriteLine($"{fs.PrintWorkingDirectory()} $ {input}");
    var output = shell.Execute(input);
    if (!string.IsNullOrEmpty(output))
        Console.WriteLine(output);
}
