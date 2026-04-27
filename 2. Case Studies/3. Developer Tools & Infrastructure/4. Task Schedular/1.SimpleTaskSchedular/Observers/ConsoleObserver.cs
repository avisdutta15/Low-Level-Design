using _1.SimpleTaskSchedular.Enums;
using _1.SimpleTaskSchedular.Interface;

namespace _1.SimpleTaskSchedular.Observers;

public class ConsoleObserver : IObserver
{
    public void OnEvent(string taskName, EventType eventType, Exception? exception = null)
    {
        Console.WriteLine($" [{eventType} {taskName}]" + (exception!=null ? $" - {exception.Message}" : " "));
    }
}
