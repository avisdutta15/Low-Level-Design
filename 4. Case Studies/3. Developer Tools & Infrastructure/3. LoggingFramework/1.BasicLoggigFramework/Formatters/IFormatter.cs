using _1.BasicLoggigFramework.Core;

namespace _1.BasicLoggigFramework.Formatters;

public interface IFormatter
{
    string Format(LogMessage message);
}
