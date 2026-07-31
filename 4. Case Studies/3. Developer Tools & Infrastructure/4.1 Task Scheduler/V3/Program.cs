// ── Demo ─────────────────────────────────────────────────────────────────────
// V3 — Multi-threaded Task Scheduler
// New: worker thread pool, CAS status transitions, concurrent data structures,
//      separate scheduler loop thread, graceful shutdown

using System.Collections.Concurrent;
using System.Collections.Immutable;
using V3;

var scheduler = new Scheduler(workerCount: 4);
scheduler.RegisterObserver(new ConsoleObserver());

Console.WriteLine("=== V3: Multi-threaded Scheduler ===\n");

// --- Scenario 1: Diamond DAG  A → B, A → C, B+C → D (B and C run in parallel)
scheduler.Schedule(id: "A", name: "Task-A", action: () =>
{
    Thread.Sleep(200);
    Console.WriteLine("  → A done");
}, at: DateTime.UtcNow);

scheduler.Schedule(id: "B", name: "Task-B", action: () =>
{
    Thread.Sleep(300);
    Console.WriteLine("  → B done");
}, parentTaskIds: new() { "A" });

scheduler.Schedule(id: "C", name: "Task-C", action: () =>
{
    Thread.Sleep(100);
    Console.WriteLine("  → C done");
}, parentTaskIds: new() { "A" });

scheduler.Schedule(id: "D", name: "Task-D",
    action: () => Console.WriteLine("  → D done (after B and C)"),
    parentTaskIds: new() { "B", "C" });

// --- Scenario 2: Failure propagation
scheduler.Schedule(id: "fail", name: "Failing Task",
    action: () => throw new Exception("Boom!"), at: DateTime.UtcNow.AddSeconds(1));
scheduler.Schedule(id: "dep1", name: "Dep-1",
    action: () => Console.WriteLine("  → dep1 (should not run)"), parentTaskIds: new() { "fail" });
scheduler.Schedule(id: "dep2", name: "Dep-2",
    action: () => Console.WriteLine("  → dep2 (should not run)"), parentTaskIds: new() { "dep1" });

// --- Scenario 3: Recurring task
scheduler.Schedule(id: "hb", name: "Heartbeat",
    action: () => Console.WriteLine("  → ♥ ping"), interval: TimeSpan.FromSeconds(2));

// --- Scenario 4: Cancellation
scheduler.Schedule(id: "cancel-me", name: "Cancelled Task",
    action: () => Console.WriteLine("  → should not run"), at: DateTime.UtcNow.AddSeconds(5));
Task.Delay(500).ContinueWith(_ => scheduler.CancelTask("cancel-me"));

// Shutdown after 8 seconds
Task.Delay(8000).ContinueWith(_ =>
{
    Console.WriteLine("\n=== Initiating shutdown... ===");
    scheduler.Shutdown();
});

Thread.Sleep(9000);
Console.WriteLine("\n=== Shutdown complete ===");

// ─────────────────────────────────────────────────────────────────────────────

namespace V3
{
    // ── Enums ─────────────────────────────────────────────────────────────────

    enum TaskStatus { Pending, Enqueued, Running, Completed, Failed, Cancelled }
    enum EventType  { Started, Completed, Failed, Cancelled }

    // ── Models ────────────────────────────────────────────────────────────────

    class ScheduledTask
    {
        private int _status = (int)TaskStatus.Pending;

        public string Id { get; }
        public string Name { get; }
        public Action Action { get; }
        public DateTime ScheduledTime { get; }
        public TimeSpan? RecurrenceInterval { get; }
        public bool IsRecurring => RecurrenceInterval.HasValue;
        public List<string> ParentTaskIds { get; }
        public CancellationTokenSource Cts { get; } = new();

        // Kahn's in-degree counter — decremented atomically by Interlocked.Decrement
        public int RemainingParentsNotExecuted;

        public TaskStatus Status
        {
            get
            {
                return (TaskStatus)Volatile.Read(ref _status);
            }
        }

        // CAS: only ONE thread can win any given transition
        public bool TryTransition(TaskStatus from, TaskStatus to)
        {
            return Interlocked.CompareExchange(ref _status, (int)to, (int)from) == (int)from;
        }

        public ScheduledTask(string id, string name, Action action,
            DateTime? scheduledTime = null, TimeSpan? recurrenceInterval = null,
            List<string>? parentTaskIds = null)
        {
            Id = id; Name = name; Action = action;
            ScheduledTime = scheduledTime ?? DateTime.UtcNow;
            RecurrenceInterval = recurrenceInterval;
            ParentTaskIds = parentTaskIds ?? new();
        }
    }

    record TaskEvent(string TaskId, string TaskName, EventType EventType,
        DateTime Timestamp, Exception? Exception = null);

    // ── Observer ──────────────────────────────────────────────────────────────

    interface ITaskObserver 
    { 
        void OnEvent(TaskEvent e); 
    }

    class ConsoleObserver : ITaskObserver
    {
        private readonly object _consoleLock = new();
        public void OnEvent(TaskEvent e)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = e.EventType switch
                {
                    EventType.Started   => ConsoleColor.Cyan,
                    EventType.Completed => ConsoleColor.Green,
                    EventType.Failed    => ConsoleColor.Red,
                    EventType.Cancelled => ConsoleColor.Yellow,
                    _                   => ConsoleColor.White
                };
                var thread = Thread.CurrentThread.Name ?? "?";
                var msg = e.Exception != null ? $" — {e.Exception.Message}" : "";
                Console.WriteLine($"[{e.EventType,-10}] [{thread,-12}] {e.TaskName}{msg}");
                Console.ResetColor();
            }
        }
    }

    // ── Scheduler ─────────────────────────────────────────────────────────────

    class Scheduler
    {
        // Thread-safe task registry
        private readonly ConcurrentDictionary<string, ScheduledTask> _taskRepo = new();

        // Producer-consumer work queue — workers block here (no busy-wait)
        private readonly BlockingCollection<ScheduledTask> _queue = new();

        private readonly CancellationTokenSource _cts = new();
        private ImmutableHashSet<ITaskObserver> _observers = ImmutableHashSet<ITaskObserver>.Empty;
        private readonly Thread[] _workers;

        // Kahn's graph — all mutations protected by _graphLock
        private readonly Dictionary<string, List<string>> _adjList = new();
        private readonly object _graphLock = new();

        // Our system will have a scheduler thread and bunch of worker threads.
        // The scheduler thread will poll for ready tasks and dispatch them to queue.
        // The workers consume from the queue.
        // The division of responsibility is:
        //      WorkerLoop — owns execution lifecycle: start → run → complete/fail → reschedule
        //      SchedulerLoop — owns time-based dispatch: is it time yet? → enqueue
        public Scheduler(int workerCount = 4)
        {
            _workers = new Thread[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                _workers[i] = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = $"Worker-{i}"
                };
                _workers[i].Start();
            }

            // Separate thread polls pending tasks and enqueues time-ready ones
            var schedulerThread = new Thread(Run)
            {
                IsBackground = true,
                Name = "SchedulerLoop"
            };
            schedulerThread.Start();
        }

        public void RegisterObserver(ITaskObserver o)
        {
            ImmutableInterlocked.Update(ref _observers, s => s.Add(o));
        }

        // Single entry point: at and parentTaskIds are optional; interval makes it recurring
        public void Schedule(string id, string name, Action action,
            DateTime? at = null, TimeSpan? interval = null, List<string>? parentTaskIds = null)
        {
            Register(id, name, action, at ?? DateTime.UtcNow, interval, parentTaskIds);
        }

        public bool CancelTask(string id)
        {
            // Get the task from the repo based on id
            if (!_taskRepo.TryGetValue(id, out var task)) 
                return false;

            // Try to transition status — PENDING -> CANCELLED || SCHEDULED -> CANCELLED
            if (task.TryTransition(TaskStatus.Pending,   TaskStatus.Cancelled) ||
                task.TryTransition(TaskStatus.Enqueued, TaskStatus.Cancelled))
            {
                // Call the task's cancellation token source.
                task.Cts.Cancel();
                Notify(new TaskEvent(task.Id, task.Name, EventType.Cancelled, DateTime.UtcNow));
                PropagateFailed(id);
                return true;
            }
            return false;
        }

        public void Shutdown()
        {
            _queue.CompleteAdding();    // no new items accepted
            _cts.Cancel();              // signal loops to stop
            foreach (var w in _workers)
                w.Join(TimeSpan.FromSeconds(10));   // wait for workers to drain
        }

        // ── Kahn's Algorithm ──────────────────────────────────────────────────

        private void Register(string id, string name, Action action,
            DateTime? at = null, TimeSpan? interval = null, List<string>? parentTaskIds = null)
        {
            // Create the task object
            var task = new ScheduledTask(id, name, action, at ?? DateTime.UtcNow, interval, parentTaskIds);

            // Add the task to the repo
            if (!_taskRepo.TryAdd(task.Id, task))
                throw new InvalidOperationException($"Duplicate task id: {task.Id}");

            lock (_graphLock)
            {
                // if the node (this task) does not exist in the graph
                // then add it
                if (!_adjList.ContainsKey(task.Id))
                    _adjList[task.Id] = new();

                // We will calculate the indegree for this node
                int inDegree = 0;

                // for all the tasks on which this task depends on
                // Create a edge from depId -> task
                foreach (var parentId in task.ParentTaskIds)
                {
                    // if parent does not exist in graph then error out.
                    if (!_adjList.ContainsKey(parentId))
                        _adjList[parentId] = new();

                    // add the edge from depId -> task
                    _adjList[parentId].Add(task.Id);    // forward edge

                    // if the parent task is not yet completed then increment the indgree
                    if (!(_taskRepo.TryGetValue(parentId, out var parent) &&
                          parent.Status == TaskStatus.Completed))
                        inDegree++;
                }

                // Set the indegree
                Interlocked.Exchange(ref task.RemainingParentsNotExecuted, inDegree);
            }

            // in-degree == 0 → no waiting needed, enqueue immediately
            if (task.RemainingParentsNotExecuted == 0) 
            {
                // CAS: Pending → Enqueued — only one thread can win this
                if (task.TryTransition(TaskStatus.Pending, TaskStatus.Enqueued))
                    _queue.Add(task);
            }                
        }

        // Kahn's step: decrement in-degree of children; enqueue those that hit 0
        private void OnCompleted(string taskId)
        {
            List<string> children;
            lock (_graphLock)
            {
                if (!_adjList.TryGetValue(taskId, out children!)) 
                    return;
            }

            foreach (var childId in children)
            {
                if (!_taskRepo.TryGetValue(childId, out var child)) continue;

                // Atomic decrement — safe when multiple parents complete concurrently
                if (Interlocked.Decrement(ref child.RemainingParentsNotExecuted) == 0)
                {
                    // CAS: Pending → Enqueued — only one thread can win this
                    if (child.TryTransition(TaskStatus.Pending, TaskStatus.Enqueued))
                        _queue.Add(child);
                }
            }
        }

        // BFS failure propagation: mark all transitive children as Failed
        private void PropagateFailed(string taskId)
        {
            lock (_graphLock)
            {
                var queue = new Queue<string>();
                queue.Enqueue(taskId);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (!_adjList.TryGetValue(current, out var children)) continue;

                    foreach (var childId in children)
                    {
                        if (!_taskRepo.TryGetValue(childId, out var child)) continue;

                        // TryTransition ensures each task is only failed once
                        if (child.TryTransition(TaskStatus.Pending,   TaskStatus.Failed) ||
                            child.TryTransition(TaskStatus.Enqueued, TaskStatus.Failed))
                        {
                            Notify(new TaskEvent(child.Id, child.Name, EventType.Failed, DateTime.UtcNow,
                                new Exception("Dependency failed")));
                            queue.Enqueue(childId);
                        }
                    }
                }
            }
        }

        // ── Scheduler Loop ────────────────────────────────────────────────────
        // Polls every 100ms; enqueues time-based tasks whose scheduled time has arrived

        private void Run()
        {
            // WaitOne(100) = sleep 100ms OR wake early on cancellation
            while (!_cts.Token.WaitHandle.WaitOne(100))
            {
                var tasks = _taskRepo.Values;
                foreach (var task in tasks)
                {
                    // Task already scheduled or inprogress or completed.
                    if (task.Status != TaskStatus.Pending) 
                        continue;

                    // If the task is waiting for its parents to finish
                    if (task.RemainingParentsNotExecuted > 0) 
                        continue;

                    // Check if it is ready to be executed.
                    if (DateTime.UtcNow < task.ScheduledTime) 
                        continue;

                    // CAS: Pending → Enqueued — only one thread can win this
                    if (task.TryTransition(TaskStatus.Pending, TaskStatus.Enqueued))
                        _queue.Add(task);
                }
            }
        }

        // ── Worker Loop ───────────────────────────────────────────────────────

        private void WorkerLoop()
        {
            try
            {
                foreach (var task in _queue.GetConsumingEnumerable(_cts.Token))
                {
                    // while this task was in queue, if someone cancelled it then 
                    // skip this task
                    if (task.Cts.Token.IsCancellationRequested) 
                        continue;

                    // CAS: Enqueued → Running — only ONE worker wins this per task
                    if (!task.TryTransition(TaskStatus.Enqueued, TaskStatus.Running))
                        continue;

                    Notify(new TaskEvent(task.Id, task.Name, EventType.Started, DateTime.UtcNow));
                    try
                    {
                        task.Action();
                        task.TryTransition(TaskStatus.Running, TaskStatus.Completed);
                        Notify(new TaskEvent(task.Id, task.Name, EventType.Completed, DateTime.UtcNow));
                        OnCompleted(task.Id);   // Kahn's: unlock children

                        // Reschedule recurring tasks
                        if (task.IsRecurring && !task.Cts.Token.IsCancellationRequested)
                        {
                            Register(task.Id + "_" + DateTime.UtcNow.Ticks,
                                task.Name, task.Action,
                                DateTime.UtcNow.Add(task.RecurrenceInterval!.Value),
                                task.RecurrenceInterval);
                        }
                    }
                    catch (Exception ex)
                    {
                        task.TryTransition(TaskStatus.Running, TaskStatus.Failed);
                        Notify(new TaskEvent(task.Id, task.Name, EventType.Failed, DateTime.UtcNow, ex));
                        PropagateFailed(task.Id);
                    }
                }
            }
            catch (OperationCanceledException) { /* shutdown signal — exit cleanly */ }
        }

        private void Notify(TaskEvent e)
        {
            foreach (var o in _observers)
                try { o.OnEvent(e); } catch { }
        }
    }
}
