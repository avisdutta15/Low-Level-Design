using LoggingFramework.Core;
using LoggingFramework.Formatters;

namespace LoggingFramework.Appenders;

public abstract class AppenderBase : IAppender
{
    private IFormatter? _formatter;

    protected AppenderBase(IFormatter? formatter = null)
    {
        _formatter = formatter;
    }

    public void SetFormatter(IFormatter formatter)
    {
        _formatter = formatter;
    }

    protected string FormatMessage(LogMessage message)
    {
        return _formatter != null ? _formatter.Format(message) : message.ToString();
    }

    public abstract void Append(LogMessage message);
}
