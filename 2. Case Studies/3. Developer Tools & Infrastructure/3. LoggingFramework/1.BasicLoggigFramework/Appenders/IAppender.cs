using _1.BasicLoggigFramework.Core;

namespace _1.BasicLoggigFramework.Appenders;

public interface IAppender
{
    public void Append(LogMessage message);
}
