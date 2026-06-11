using _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Enums;
using _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Interface;

namespace _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Observers;

public class ConsoleObserver : IObserver
{
    public void OnEvent(string taskName, EventType eventType, Exception? exception = null)
    {
        Console.WriteLine($" [{eventType} {taskName}]" + (exception != null ? $" - {exception.Message}" : " "));
    }
}
