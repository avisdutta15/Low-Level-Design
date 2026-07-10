using LoggingFramework.Core;
using LoggingFramework.Formatters;

namespace LoggingFramework.Appenders;

public class ConsoleAppender : AppenderBase
{
    public ConsoleAppender(IFormatter? formatter = null) : base(formatter) { }

    public override void Append(LogMessage message)
    {
        Console.WriteLine(FormatMessage(message));
    }
}
