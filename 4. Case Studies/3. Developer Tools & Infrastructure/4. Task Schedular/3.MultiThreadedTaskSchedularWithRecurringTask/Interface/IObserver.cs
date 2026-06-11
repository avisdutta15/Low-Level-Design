using _3.MultiThreadedTaskSchedularWithRecurringTask.Enums;

namespace _3.MultiThreadedTaskSchedularWithRecurringTask.Interface;

public interface IObserver
{
    void OnEvent(string taskName, EventType eventType, Exception? exception = null);
}
