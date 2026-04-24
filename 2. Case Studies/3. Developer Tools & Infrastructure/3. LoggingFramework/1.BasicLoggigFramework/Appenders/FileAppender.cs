using _1.BasicLoggigFramework.Core;
using _1.BasicLoggigFramework.Formatters;

namespace _1.BasicLoggigFramework.Appenders;

public class FileAppender : AppenderBase, IDisposable
{
    private readonly string _logDirectory;
    private readonly StreamWriter _writer;

    public FileAppender(string logDirectory = "./logs", IFormatter? formatter = null) 
        : base(formatter)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filePath = Path.Combine(_logDirectory, $"app_{timestamp}.log");
        _writer = new StreamWriter(filePath, append: true);
    }

    public override void Append(LogMessage message)
    {
        _writer.WriteLine(FormatMessage(message));
        _writer.Flush();
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
