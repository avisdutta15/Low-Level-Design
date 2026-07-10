using LoggingFramework.Core;

namespace LoggingFramework.Appenders;

public interface IAppender
{
    public void Append(LogMessage message);
}
