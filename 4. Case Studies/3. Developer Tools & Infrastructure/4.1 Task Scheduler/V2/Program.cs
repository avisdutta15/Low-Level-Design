// ── Demo ─────────────────────────────────────────────────────────────────────
// V2 — Single-threaded Task Scheduler + Dependency Tracking (Kahn's Algorithm)
// New: tasks declare dependencies; Kahn's in-degree tracking; BFS failure propagation

using V2;

var scheduler = new Scheduler();
scheduler.RegisterObserver(new ConsoleObserver());

Console.WriteLine("=== V2: Single-threaded Scheduler + Dependencies ===\n");

// --- Scenario 1: Linear chain  build → test → deploy
scheduler.Schedule(id: "build", name: "Build", action: () => Console.WriteLine("  → Building..."), at: DateTime.UtcNow);
scheduler.Schedule(id: "test", name: "Test", action: () => Console.WriteLine("  → Testing..."), parentTaskIds: new List<string> { "build" });
scheduler.Schedule(id: "deploy", name: "Deploy", action: () => Console.WriteLine("  → Deploying..."), parentTaskIds: new List<string> { "test" });

// --- Scenario 2: Diamond  A → B, A → C, B+C → D
scheduler.Schedule(id: "A", name: "Task-A", action: () => Console.WriteLine("  → A"), at: DateTime.UtcNow.AddSeconds(1));
scheduler.Schedule(id: "B", name: "Task-B", action: () => Console.WriteLine("  → B"), parentTaskIds: new List<string> { "A" });
scheduler.Schedule(id: "C", name: "Task-C", action: () => Console.WriteLine("  → C"), parentTaskIds: new List<string> { "A" });
scheduler.Schedule(id: "D", name: "Task-D", action: () => Console.WriteLine("  → D"), parentTaskIds: new List<string> { "B", "C" });

// --- Scenario 3: Failure propagation  fail → dep1 → dep2
scheduler.Schedule(id: "fail", name: "Failing Task",
    action: () => throw new Exception("Boom!"), at: DateTime.UtcNow.AddSeconds(2));
scheduler.Schedule(id: "dep1", name: "Dep-1", action: () => Console.WriteLine("  → dep1"), parentTaskIds: new List<string> { "fail" });
scheduler.Schedule(id: "dep2", name: "Dep-2", action: () => Console.WriteLine("  → dep2"), parentTaskIds: new List<string> { "dep1" });

// --- Scenario 4: Recurring
scheduler.Schedule(id: "hb", name: "Heartbeat", action: () => Console.WriteLine("  → ♥ ping"), interval: TimeSpan.FromSeconds(2));

// Shutdown after 8 seconds
Task.Delay(8000).ContinueWith(_ => scheduler.Shutdown());

scheduler.Run();

Console.WriteLine("\n=== Shutdown complete ===");

// ─────────────────────────────────────────────────────────────────────────────

namespace V2
{
    // ── Enums ─────────────────────────────────────────────────────────────────

    enum TaskStatus { Pending, Running, Completed, Failed, Cancelled }
    enum EventType { Started, Completed, Failed, Cancelled }

    // ── Models ────────────────────────────────────────────────────────────────

    class ScheduledTask
    {
        public string Id { get; }
        public string Name { get; }
        public Action Action { get; }
        public DateTime ScheduledTime { get; set; }
        public TimeSpan? RecurrenceInterval { get; }
        public bool IsRecurring => RecurrenceInterval.HasValue;
        public TaskStatus Status { get; set; } = TaskStatus.Pending;
        public List<string> ParentTaskIds { get; }

        // Kahn's in-degree counter: how many parents haven't completed yet
        public int RemainingParentsNotExecuted { get; set; }

        public ScheduledTask(string id, string name, Action action,
            DateTime? scheduledTime = null, TimeSpan? recurrenceInterval = null,
            List<string>? parentTaskIds = null)
        {
            Id = id;
            Name = name;
            Action = action;
            ScheduledTime = scheduledTime ?? DateTime.UtcNow;
            RecurrenceInterval = recurrenceInterval;
            ParentTaskIds = parentTaskIds ?? new();
        }
    }

    record TaskEvent(string TaskId, string TaskName, EventType EventType,
        DateTime Timestamp, Exception? Exception = null);

    // ── Observer ──────────────────────────────────────────────────────────────

    interface ITaskObserver { void OnEvent(TaskEvent e); }

    class ConsoleObserver : ITaskObserver
    {
        public void OnEvent(TaskEvent e)
        {
            Console.ForegroundColor = e.EventType switch
            {
                EventType.Started => ConsoleColor.Cyan,
                EventType.Completed => ConsoleColor.Green,
                EventType.Failed => ConsoleColor.Red,
                EventType.Cancelled => ConsoleColor.Yellow,
                _ => ConsoleColor.White
            };
            var msg = e.Exception != null ? $" — {e.Exception.Message}" : "";
            Console.WriteLine($"[{e.EventType,-10}] {e.TaskName}{msg}");
            Console.ResetColor();
        }
    }

    // ── Scheduler ─────────────────────────────────────────────────────────────

    class Scheduler
    {
        private readonly Dictionary<string, ScheduledTask> _taskRepo = new();
        private readonly Dictionary<string, List<string>> _adjList = new();
        private readonly List<ITaskObserver> _observers = new();
        private bool _isCancelled = false;

        public void RegisterObserver(ITaskObserver o) => _observers.Add(o);

        // Single entry point: at and parentTaskIds are optional; interval makes it recurring
        public void Schedule(string id, string name, Action action,
            DateTime? at = null, TimeSpan? interval = null, List<string>? parentTaskIds = null)
        {
            // Create the task object
            var task = new ScheduledTask(
                id, name, action,
                scheduledTime: at ?? DateTime.UtcNow,
                recurrenceInterval: interval,
                parentTaskIds: parentTaskIds);

            // Add the task to the repo
            _taskRepo[task.Id] = task;

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
                    throw new InvalidOperationException($"Unknown dependency '{parentId}': schedule it before '{id}'.");

                // add the edge from depId -> task
                _adjList[parentId].Add(task.Id);

                // if the parent task is not yet completed then increment the indgree
                if (_taskRepo[parentId].Status != TaskStatus.Completed)
                    inDegree++;
            }

            // Set the indegree
            task.RemainingParentsNotExecuted = inDegree;
        }

        public bool CancelTask(string id)
        {
            if (!_taskRepo.TryGetValue(id, out var task)) return false;
            if (task.Status != TaskStatus.Pending) return false;

            task.Status = TaskStatus.Cancelled;
            Notify(new TaskEvent(task.Id, task.Name, EventType.Cancelled, DateTime.UtcNow));
            PropagateFailed(id);    // cancelled = will never complete → fail children

            return true;
        }

        // Single-threaded scheduler loop.
        // After every iteration, it sleeps for 100ms to avoid busy-waiting.
        public void Run()
        {
            while (_isCancelled == false)
            {
                var tasks = _taskRepo.Values.ToList();

                // Iterate over the list of tasks
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

                    // Execute once.
                    // Before execution, change status and notify
                    Execute(task);

                    // Reschedule recurring tasks
                    if (task.IsRecurring && task.Status == TaskStatus.Completed)
                    {
                        task.ScheduledTime = DateTime.UtcNow.Add(task.RecurrenceInterval!.Value);
                        task.Status = TaskStatus.Pending;
                    }
                }

                Thread.Sleep(100);
            }
        }

        public void Shutdown() => _isCancelled = true;

        // ── Kahn's Algorithm ──────────────────────────────────────────────────

        // Decrement in-degree of all children; hits 0 → scheduler loop picks it up
        private void OnCompleted(string taskId)
        {
            // Get all the child tasks and decrement their in-degree
            foreach (var childId in _adjList[taskId])
            {
                _taskRepo[childId].RemainingParentsNotExecuted--;
            }
        }

        // BFS failure propagation: mark all children as Failed
        private void PropagateFailed(string taskId)
        {
            // Enqueue the current node
            var queue = new Queue<string>();
            queue.Enqueue(taskId);

            // BFS over the children nodes
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                // for all the children, update status and enqueue them
                foreach (var childId in _adjList[current])
                {
                    var childTask = _taskRepo[childId];

                    // Skip if the child is either running / failed / completed
                    if (childTask.Status != TaskStatus.Pending)
                        continue;

                    childTask.Status = TaskStatus.Failed;
                    Notify(new TaskEvent(childTask.Id, childTask.Name, EventType.Failed, DateTime.UtcNow,
                        new Exception("Dependency failed")));
                    queue.Enqueue(childId);
                }
            }
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void Execute(ScheduledTask task)
        {
            // Pending -> Running
            task.Status = TaskStatus.Running;
            Notify(new TaskEvent(task.Id, task.Name, EventType.Started, DateTime.UtcNow));
            try
            {
                // Execute the task
                task.Action();

                // Running -> Completed
                task.Status = TaskStatus.Completed;
                Notify(new TaskEvent(task.Id, task.Name, EventType.Completed, DateTime.UtcNow));
                OnCompleted(task.Id);   // Kahn's: unlock children
            }
            catch (Exception ex)
            {
                // Running -> Failed
                task.Status = TaskStatus.Failed;
                Notify(new TaskEvent(task.Id, task.Name, EventType.Failed, DateTime.UtcNow, ex));
                PropagateFailed(task.Id);
            }
        }

        private void Notify(TaskEvent e)
        {
            foreach (var o in _observers)
                try { o.OnEvent(e); } catch { }
        }
    }
}
