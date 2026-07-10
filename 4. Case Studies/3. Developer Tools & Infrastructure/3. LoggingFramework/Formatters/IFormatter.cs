using LoggingFramework.Core;

namespace LoggingFramework.Formatters;

public interface IFormatter
{
    string Format(LogMessage message);
}
