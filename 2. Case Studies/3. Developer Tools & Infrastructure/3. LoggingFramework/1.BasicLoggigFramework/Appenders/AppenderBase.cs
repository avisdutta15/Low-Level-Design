using _1.BasicLoggigFramework.Core;
using _1.BasicLoggigFramework.Formatters;

namespace _1.BasicLoggigFramework.Appenders;

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
