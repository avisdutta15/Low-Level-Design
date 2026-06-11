using _1.BasicLoggigFramework.Core;

namespace _1.BasicLoggigFramework.Formatters;

public class TextFormatter : IFormatter
{
    public string Format(LogMessage message)
    {
        if (message.Exception == null)
            return $"[{message.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}] - [{message.Level}] - [{message.Message}]";
        return $"[{message.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}] - [{message.Level}] - [{message.Message}] - [Exception: {message.Exception}]";
    }
}
