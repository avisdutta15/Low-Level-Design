using _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Enums;

namespace _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Interface;

public interface IObserver
{
    void OnEvent(string taskName, EventType eventType, Exception? exception = null);
}
