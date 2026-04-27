using TaskStatus = _3.MultiThreadedTaskSchedularWithRecurringTask.Enums.TaskStatus;

namespace _3.MultiThreadedTaskSchedularWithRecurringTask.Entities;

public class ScheduledTask
{
    private int _status;
    public string Id { get; private set; }
    public string Name { get; private set; }
    public Action Work {  get; private set; }
    public DateTimeOffset? ScheduledTime { get; private set; }
    public TimeSpan? RecurringInterval { get; private set; }


    public TaskStatus Status => (TaskStatus)Volatile.Read(ref _status);

    public ScheduledTask(string id
        , string name
        , Action work
        , DateTimeOffset? scheduledTime = null
        , TimeSpan? recurringInterval = null )
    {
        Id = id;
        Name = name;
        Work = work;
        _status = (int)TaskStatus.Pending;  // By default make it Pending
        ScheduledTime = scheduledTime;
        RecurringInterval = recurringInterval;
    }

    public bool TryTransition(TaskStatus from, TaskStatus to)
    {
        return Interlocked.CompareExchange(ref _status, (int)to, (int)from) == (int)from;
    }
}
