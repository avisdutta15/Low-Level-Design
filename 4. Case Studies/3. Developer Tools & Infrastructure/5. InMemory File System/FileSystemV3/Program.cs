using FileSystemV3.FileSystem;
using FileSystemV3.Shell;
using FileSystemV3.Shell.Commands;

// ─────────────────────────────────────────────────────────────────────
// V3: Thread-safe In-Memory File System
// - ConcurrentDictionary for directory children (atomic TryAdd)
// - lock for FileNode Read/Write (mutual exclusion)
// - lock for CurrentDirectory (visibility across threads)
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
// Demo: concurrent writes to demonstrate thread safety
// ─────────────────────────────────────────────────────────────────────
Console.WriteLine("=== In-Memory File System V3 (Thread-Safe) ===\n");

// Setup directory structure
shell.Execute("mkdir /data");
Console.WriteLine("Created /data");

// Create multiple files concurrently
Console.WriteLine("\n--- Concurrent file creation (10 threads) ---");
var tasks = new Task[10];
for (int i = 0; i < 10; i++)
{
    int index = i;
    tasks[i] = Task.Run(() =>
    {
        string fileName = $"/data/file_{index}.txt";
        shell.Execute($"touch {fileName}");
        shell.Execute($"echo \"content from thread {index}\" > {fileName}");
    });
}
Task.WaitAll(tasks);

// List all files created
Console.WriteLine("\nFiles created:");
var output = shell.Execute("ls -l /data");
Console.WriteLine(output);

// Read files concurrently
Console.WriteLine("\n--- Concurrent file reads (10 threads) ---");
var readTasks = new Task<string>[10];
for (int i = 0; i < 10; i++)
{
    int index = i;
    readTasks[i] = Task.Run(() =>
    {
        return shell.Execute($"cat /data/file_{index}.txt");
    });
}
Task.WaitAll(readTasks);

for (int i = 0; i < 10; i++)
{
    Console.WriteLine($"  file_{i}.txt: {readTasks[i].Result}");
}

// ─────────────────────────────────────────────────────────────────────
// Interactive REPL
// ─────────────────────────────────────────────────────────────────────
Console.WriteLine("\n=== Interactive Shell ===\n");

while (true)
{
    Console.Write($"{fs.PrintWorkingDirectory()} $ ");
    var input = Console.ReadLine();
    if (input == null || input.Trim() == "exit") break;

    var result = shell.Execute(input);
    if (!string.IsNullOrEmpty(result))
        Console.WriteLine(result);
}
