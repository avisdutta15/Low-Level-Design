using _3.MultiThreadedTaskSchedularWithRecurringTask.Enums;
using _3.MultiThreadedTaskSchedularWithRecurringTask.Interface;

namespace _3.MultiThreadedTaskSchedularWithRecurringTask.Observers;

public class ConsoleObserver : IObserver
{
    public void OnEvent(string taskName, EventType eventType, Exception? exception = null)
    {
        Console.WriteLine($" [{eventType} {taskName}]" + (exception!=null ? $" - {exception.Message}" : " "));
    }
}
