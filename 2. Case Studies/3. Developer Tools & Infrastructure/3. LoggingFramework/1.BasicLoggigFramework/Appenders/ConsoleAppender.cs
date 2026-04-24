using _1.BasicLoggigFramework.Core;
using _1.BasicLoggigFramework.Formatters;

namespace _1.BasicLoggigFramework.Appenders;

public class ConsoleAppender : AppenderBase
{
    public ConsoleAppender(IFormatter? formatter = null) : base(formatter) { }

    public override void Append(LogMessage message)
    {
        Console.WriteLine(FormatMessage(message));
    }
}
