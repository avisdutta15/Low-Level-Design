using _1.SimpleTaskSchedular.Enums;

namespace _1.SimpleTaskSchedular.Interface;

public interface IObserver
{
    void OnEvent(string taskName, EventType eventType, Exception? exception = null);
}
