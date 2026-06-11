using _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Enums;
using _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Interface;
using System.Collections.Concurrent;
using TaskStatus = _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Enums.TaskStatus;

namespace _4.MultiThreadedTaskSchedularWithRecurringAndDependency.Entities;

// Builds on project 3 with:
// - Dependency graph via Kahn's topological sort
// - BFS failure propagation through dependents
// - Separate adjacency list for clean graph storage
public class TaskScheduler : IDisposable
{
    private readonly ConcurrentDictionary<string, ScheduledTask> _tasks = new();
    private readonly BlockingCollection<ScheduledTask> _queue = new();
    private readonly Thread[] _workers;
    private readonly Thread _pollLoopThread;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentBag<IObserver> _observers = new();

    // Dependency graph — adjacency list: taskId → list of dependent task IDs
    private readonly ConcurrentDictionary<string, List<string>> _dependents = new();

    public TaskScheduler(int workerCount)
    {
        _workers = new Thread[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            _workers[i] = new Thread(WorkerLoop) { IsBackground = true };
            _workers[i].Start();
        }

        _pollLoopThread = new Thread(PollLoop) { IsBackground = true };
        _pollLoopThread.Start();
    }

    public void AddObservers(IObserver observer)
    {
        _observers.Add(observer);
    }

    // Submit without dependencies
    public void Submit(string id, string name, Action work,
        DateTimeOffset? runAt = null, TimeSpan? interval = null)
        => Register(new ScheduledTask(id, name, work, runAt, interval));

    // Submit with dependencies
    public void SubmitWithDeps(string id, string name, Action work, List<string> deps,
        DateTimeOffset? runAt = null, TimeSpan? interval = null)
        => Register(new ScheduledTask(id, name, work, runAt, interval, deps));

    // Wire dependency edges, compute in-degree, and try to enqueue
    private void Register(ScheduledTask task)
    {
        _tasks[task.Id] = task;
        _dependents.TryAdd(task.Id, new List<string>());

        int inDegree = 0;
        foreach (var depId in task.DependsOn)
        {
            // Wire forward edge: depId → this task
            _dependents.GetOrAdd(depId, _ => new List<string>()).Add(task.Id);

            // Count only unfinished dependencies
            if (!_tasks.TryGetValue(depId, out var dep) || dep.Status != TaskStatus.Completed)
                inDegree++;
        }
        Interlocked.Exchange(ref task.RemainingDeps, inDegree);
        TryEnqueue(task);
    }

    // Enqueue only if all dependencies are met and ready to run
    private void TryEnqueue(ScheduledTask task)
    {
        if (Volatile.Read(ref task.RemainingDeps) == 0 && IsReady(task))
            if (task.TryTransition(TaskStatus.Pending, TaskStatus.Scheduled))
                _queue.Add(task);
    }

    // A task is ready if it has no scheduled time, or the scheduled time has passed
    private bool IsReady(ScheduledTask task)
    {
        return !task.RunAt.HasValue || task.RunAt.Value <= DateTimeOffset.UtcNow;
    }

    public bool Cancel(string id)
    {
        if (!_tasks.TryGetValue(id, out var task)) return false;
        if (!task.TryTransition(TaskStatus.Pending, TaskStatus.Cancelled) &&
            !task.TryTransition(TaskStatus.Scheduled, TaskStatus.Cancelled))
            return false;
        Notify(task.Name, EventType.Cancelled);
        PropagateFail(task.Id);     // dependents can never run
        return true;
    }

    public void Shutdown()
    {
        _cts.Cancel();
        for (int i = 0; i < _workers.Length; i++)
            _workers[i].Join(TimeSpan.FromSeconds(5));
        Console.WriteLine("Shutting Down!");
    }

    public void Dispose()
    {
        Shutdown();
        _queue.Dispose();
        _cts.Dispose();
    }

    private void PollLoop()
    {
        while (!_cts.Token.WaitHandle.WaitOne(1000))
        {
            foreach (var pair in _tasks)
            {
                var task = pair.Value;
                if (task.Status == TaskStatus.Pending
                    && Volatile.Read(ref task.RemainingDeps) == 0)
                {
                    TryEnqueue(task);
                }
            }
        }
    }

    private void WorkerLoop()
    {
        try
        {
            foreach (var task in _queue.GetConsumingEnumerable(_cts.Token))
            {
                // CAS: Scheduled → Running (skip if cancelled while in queue)
                if (!task.TryTransition(TaskStatus.Scheduled, TaskStatus.Running))
                    continue;

                Notify(task.Name, EventType.Started);

                try
                {
                    task.Work();
                    task.TryTransition(TaskStatus.Running, TaskStatus.Completed);
                    Notify(task.Name, EventType.Completed);

                    // Kahn's step: decrement in-degree of dependents
                    if (_dependents.TryGetValue(task.Id, out var deps))
                        foreach (var depId in deps)
                            if (_tasks.TryGetValue(depId, out var dep))
                                if (Interlocked.Decrement(ref dep.RemainingDeps) == 0)
                                    TryEnqueue(dep);

                    // Recurring: register new instance with unique ID
                    if (task.Interval.HasValue)
                        Register(new ScheduledTask(
                            $"{task.Id}_{DateTimeOffset.UtcNow.Ticks}",
                            task.Name, task.Work,
                            DateTimeOffset.UtcNow + task.Interval.Value,
                            task.Interval));
                }
                catch (Exception ex)
                {
                    task.TryTransition(TaskStatus.Running, TaskStatus.Failed);
                    Notify(task.Name, EventType.Failed, ex);
                    PropagateFail(task.Id);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    // BFS failure propagation through dependency graph
    private void PropagateFail(string taskId)
    {
        var queue = new Queue<string>();
        queue.Enqueue(taskId);
        while (queue.Count > 0)
        {
            if (!_dependents.TryGetValue(queue.Dequeue(), out var deps))
                continue;
            foreach (var depId in deps)
            {
                if (_tasks.TryGetValue(depId, out var dep))
                {
                    if (dep.TryTransition(TaskStatus.Pending, TaskStatus.Failed) ||
                        dep.TryTransition(TaskStatus.Scheduled, TaskStatus.Failed))
                    {
                        Notify(dep.Name, EventType.Failed);
                        queue.Enqueue(depId);
                    }
                }
            }
        }
    }

    private void Notify(string taskName, EventType eventType, Exception? exception = null)
    {
        foreach (var observer in _observers)
        {
            Task.Run(() =>
            {
                try { observer.OnEvent(taskName, eventType, exception); }
                catch { }
            });
        }
    }
}
