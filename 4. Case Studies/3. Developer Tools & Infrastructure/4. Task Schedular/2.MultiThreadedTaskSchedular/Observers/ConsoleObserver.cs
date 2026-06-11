using _2.MultiThreadedTaskSchedular.Enums;
using _2.MultiThreadedTaskSchedular.Interface;

namespace _2.MultiThreadedTaskSchedular.Observers;

public class ConsoleObserver : IObserver
{
    public void OnEvent(string taskName, EventType eventType, Exception? exception = null)
    {
        Console.WriteLine($" [{eventType} {taskName}]" + (exception!=null ? $" - {exception.Message}" : " "));
    }
}
