using _1.SimpleTaskSchedular.Enums;
using _1.SimpleTaskSchedular.Interface;
using TaskStatus = _1.SimpleTaskSchedular.Enums.TaskStatus;

namespace _1.SimpleTaskSchedular.Entities;


// Uses dictionary as task store
// Uses Queue to run jobs / tasks one by one
// Uses 1 worker thread to process the tasks.
// Uses a single variable _shutdown to determine if the service is shuting down.
public class TaskScheduler
{
    private Dictionary<string, ScheduledTask> _tasks = new();
    private Queue<ScheduledTask> _queue = new();
    private Thread _worker;
    private bool _shutdown;
    private List<IObserver> _observers = new();

    public TaskScheduler()
    {
        // Start the worker thread to process the tasks by reading from the Queue
        _worker = new Thread(WorkerLoop);
        _worker.Start();
        _shutdown = false;
    }

    public void AddObservers(IObserver observer)
    {
        _observers.Add(observer);
    }
    public void Submit(string id, string name, Action work)
    {
        // Create a scheduled task.
        ScheduledTask task = new(id, name, work);

        // Add it to the task store for O(1) lookup
        _tasks[task.Id] = task;

        // Add it to the task queue for task processing
        _queue.Enqueue(task);
        task.Status = TaskStatus.Scheduled;
    }
    public bool Cancel(string id)
    {
        // If task does not exist in the task store return false.
        if(_tasks.TryGetValue(id, out var task)==false)
            return false;

        // If task is already running / completed / failed, then we can't cancel it. return false.
        if(task?.Status == TaskStatus.Running 
        || task?.Status == TaskStatus.Completed
        || task?.Status == TaskStatus.Failed)
            return false;
        
        // Set the status to Cancelled and Notify. return true.
        task!.Status = TaskStatus.Cancelled;
        Notify(task.Name, EventType.Cancelled, null);
        return true;
    }
    public void Shutdown()
    {
        _shutdown = true;
        _worker.Join(TimeSpan.FromSeconds(5));
        Console.WriteLine("Shutting Down!");
    }

    private void WorkerLoop()
    {
        while (_shutdown == false)
        {
            // If task queue is empty then sleep
            if (_queue.Count == 0)
            {
                Thread.Sleep(500);
                continue;   //After waking up check if shutdown called
            }
            
            // Dequeue the task
            var task = _queue.Dequeue();

            // If this task was cancelled by the user, then skip it.
            if (task.Status == TaskStatus.Cancelled)
                continue;

            // Change the status to Running and Notify
            task.Status = TaskStatus.Running;
            Notify(task.Name, EventType.Started, null);

            try
            {
                // Run the task and notify observers. On Completion, set Status to Completed.
                task.Work();
                task.Status = TaskStatus.Completed;
                Notify(task.Name, EventType.Completed, null);
            }
            catch (Exception ex)
            {
                // On Exception, set Status to Failed.
                task.Status = TaskStatus.Failed;
                Notify(task.Name, EventType.Failed, ex);
            }
        }
    }
    private void Notify(string taskName, EventType eventType, Exception? exception = null)
    {
        foreach (var ob in _observers)
        {
            try
            {
                ob.OnEvent(taskName, eventType, exception);
            }
            catch (Exception ex) { }
        }
    }
}
