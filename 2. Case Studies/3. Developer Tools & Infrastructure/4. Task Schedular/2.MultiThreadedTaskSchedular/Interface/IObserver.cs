using _2.MultiThreadedTaskSchedular.Enums;

namespace _2.MultiThreadedTaskSchedular.Interface;

public interface IObserver
{
    void OnEvent(string taskName, EventType eventType, Exception? exception = null);
}
