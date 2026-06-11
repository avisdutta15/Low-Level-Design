using TaskStatus = _2.MultiThreadedTaskSchedular.Enums.TaskStatus;

namespace _2.MultiThreadedTaskSchedular.Entities;

public class ScheduledTask
{
    private int _status;
    public string Id { get; private set; }
    public string Name { get; private set; }
    public Action Work {  get; private set; }

    public TaskStatus Status => (TaskStatus)Volatile.Read(ref _status);

    public ScheduledTask(string id, string name, Action work)
    {
        Id = id;
        Name = name;
        Work = work;
        _status = (int)TaskStatus.Pending;
    }

    public bool TryTransition(TaskStatus from, TaskStatus to)
    {
        return Interlocked.CompareExchange(ref _status, (int)to, (int)from) == (int)from;
    }
}
