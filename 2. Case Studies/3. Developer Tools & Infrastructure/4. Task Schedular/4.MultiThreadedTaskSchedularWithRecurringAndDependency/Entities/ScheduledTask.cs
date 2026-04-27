using TaskStatus = _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Enums.TaskStatus;

namespace _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Entities;

public class ScheduledTask
{
    private int _status = (int)TaskStatus.Pending;

    public string Id { get; }
    public string Name { get; }
    public Action Work { get; }
    public DateTimeOffset? RunAt { get; }
    public TimeSpan? Interval { get; }
    public List<string> DependsOn { get; }
    public int RemainingDeps;               // in-degree for Kahn's

    public TaskStatus Status => (TaskStatus)Volatile.Read(ref _status);

    public ScheduledTask(string id, string name, Action work,
        DateTimeOffset? runAt = null, TimeSpan? interval = null, List<string>? deps = null)
    {
        Id = id;
        Name = name;
        Work = work;
        RunAt = runAt;
        Interval = interval;
        DependsOn = deps ?? new();
    }

    public bool TryTransition(TaskStatus from, TaskStatus to)
        => Interlocked.CompareExchange(ref _status, (int)to, (int)from) == (int)from;
}
