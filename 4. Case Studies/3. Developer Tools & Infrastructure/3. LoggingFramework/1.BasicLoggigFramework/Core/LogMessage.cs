namespace _1.BasicLoggigFramework.Core;

public class LogMessage
{
    public DateTime TimeStamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; }
    public Exception? Exception { get; set; }

    public LogMessage(LogLevel level, string message, Exception? exception = null)
    {
        TimeStamp = DateTime.Now;
        Level = level;
        Message = message;
        Exception = exception;
    }

    public override string ToString()
    {
        return $"[{TimeStamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] {Message}";
    }
}
