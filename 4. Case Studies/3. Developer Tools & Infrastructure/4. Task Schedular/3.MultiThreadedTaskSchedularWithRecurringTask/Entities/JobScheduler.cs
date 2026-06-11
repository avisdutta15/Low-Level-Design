using _3.MultiThreadedTaskSchedularWithRecurringTask.Enums;
using _3.MultiThreadedTaskSchedularWithRecurringTask.Interface;
using System.Collections.Concurrent;
using TaskStatus = _3.MultiThreadedTaskSchedularWithRecurringTask.Enums.TaskStatus;

namespace _3.MultiThreadedTaskSchedularWithRecurringTask.Entities;

// Uses dictionary -> ConcurrentDictionary as task store
// Uses Queue -> BlockingCollection to run jobs / tasks one by one
// Uses N worker threads to process the tasks.
// Uses a dedicated thread to check if a task is ready to be scheduled (in case of recurring)
// Uses Cancellation Token to determine if the scheduler is shutting down or not.
public class TaskScheduler
{
    private ConcurrentDictionary<string, ScheduledTask> _tasks = new();
    private BlockingCollection<ScheduledTask> _queue = new();
    private Thread[] _workers;
    private Thread _pollLoopThread;
    private CancellationTokenSource _cts = new();
    private List<IObserver> _observers = new();
    private readonly object _lock = new();

    public TaskScheduler(int workerCount)
    {
        // Start the worker threads to process the tasks by reading from the Queue
        _workers = new Thread[workerCount];
        for (int i = 0; i < workerCount; i++) 
        {
            _workers[i] = new Thread(WorkerLoop)
            {
                IsBackground = true,
            };
            _workers[i].Start();
        }

        // Start a dedicated thread to check if a task is ready for scheduling
        _pollLoopThread = new Thread(PollLoop) 
        { 
            IsBackground = true 
        };
        _pollLoopThread.Start();
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

        //Make both enqueue and status change thread safe
        //_queue.Add(task);                     
        //task.Status = TaskStatus.Pending
        TryEnqueue(task);
    }
    public void Submit(string id, string name, Action work, DateTimeOffset scheduledTime, TimeSpan? recurringInterval = null)
    {
        ScheduledTask task = new(id, name, work, scheduledTime, recurringInterval);
        _tasks[task.Id] = task;
        // Don't enqueue immediately — PollLoop will pick it up when scheduledTime arrives
    }
    public bool Cancel(string id)
    {
        if(_tasks.TryGetValue(id, out var task) == false)
            return false;

        // Try cancelling from Pending (not yet queued) or Scheduled (in queue)
        if (task!.TryTransition(TaskStatus.Pending, TaskStatus.Cancelled) == true ||
            task!.TryTransition(TaskStatus.Scheduled, TaskStatus.Cancelled) == true)
        {
            Notify(task.Name, EventType.Cancelled, null);
            return true;
        }
        return false;
    }
    public void Shutdown()
    {
        _cts.Cancel();
        for(int i=0; i<_workers.Length; i++)
            _workers[i].Join(TimeSpan.FromSeconds(5));      //Why This Join?

        Console.WriteLine("Shutting Down!");
    }

    private bool TryEnqueue(ScheduledTask task)
    {
        if (task.TryTransition(TaskStatus.Pending, TaskStatus.Scheduled))   //CAS!
        {
            _queue.Add(task);
            return true;
        }
        return false;
    }
    private void PollLoop()
    {
        while (!_cts.Token.WaitHandle.WaitOne(1000))    //Wake up every second to check
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _tasks)
            {
                var task = pair.Value;
                if (task.Status == TaskStatus.Pending && task.ScheduledTime <= now)
                {
                    TryEnqueue(task);
                }
            }
        }
    }
    private void WorkerLoop()
    {
        foreach(var task in _queue.GetConsumingEnumerable(_cts.Token)) 
        {
            // CAS: Scheduled → Running (skip if cancelled while in queue)
            if (task.TryTransition(TaskStatus.Scheduled, TaskStatus.Running) == false)
                continue;

            Notify(task.Name, EventType.Started, null);

            try
            {
                task.Work();
                task.TryTransition(TaskStatus.Running, TaskStatus.Completed);
                Notify(task.Name, EventType.Completed, null);

                // Recurring: Re-register the task with new instance.
                if (task.RecurringInterval.HasValue)
                {
                    var newTask = new ScheduledTask(
                          task.Id
                        , task.Name
                        , task.Work
                        , DateTimeOffset.UtcNow.Add(task.RecurringInterval.Value)
                        , task.RecurringInterval);
                    _tasks[newTask.Id] = newTask;
                    // Don't TryEnqueue — let PollLoop pick it up at the right time
                }
            }
            catch (Exception ex)
            {
                task.TryTransition(TaskStatus.Running, TaskStatus.Failed);
                Notify(task.Name, EventType.Failed, ex);
            }
        }
    }
    private void Notify(string taskName, EventType eventType, Exception? exception = null)
    {
        List<IObserver> snapshot;
        lock (_lock)
        {
            snapshot = _observers.ToList();
        }
        foreach (var observer in snapshot)
        {
            // Fire and Forget
            Task.Run(() =>
            {
                try
                {
                    observer.OnEvent(taskName, eventType, exception);
                }
                catch (Exception ex) { }
            });           
        }
    }
}
