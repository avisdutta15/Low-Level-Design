using TaskStatus = _1.SimpleTaskSchedular.Enums.TaskStatus;

namespace _1.SimpleTaskSchedular.Entities;

public class ScheduledTask
{
    public string Id { get; private set; }
    public string Name { get; private set; }
    public Action Work {  get; private set; }

    public TaskStatus Status { get; set; }

    public ScheduledTask(string id, string name, Action work)
    {
        Id = id;
        Name = name;
        Work = work;
        Status = TaskStatus.Pending;
    }
}
