// ── Demo ─────────────────────────────────────────────────────────────────────
// V1.1 — Single-threaded Task Scheduler with Builder Pattern
// Features: one-time tasks, recurring tasks, cancel, observer notifications

using V1_1;

var scheduler = new Scheduler();
scheduler.RegisterObserver(new ConsoleObserver());

Console.WriteLine("=== V1.1: Single-threaded Scheduler (Builder Pattern) ===\n");

// One-time task: runs after 1 second
scheduler.Schedule("t1", "One-Time Report",
    () => { Console.WriteLine("  → Generating report..."); Thread.Sleep(200); })
    .At(DateTime.UtcNow.AddSeconds(1))
    .Build();

// Recurring task: runs every 2 seconds
scheduler.Schedule("hb", "Heartbeat",
    () => Console.WriteLine("  → ♥ ping"))
    .At(DateTime.UtcNow)
    .Recurring(TimeSpan.FromSeconds(2))
    .Build();

// Task that will be cancelled before it runs
scheduler.Schedule("t2", "Cancelled Task",
    () => Console.WriteLine("  → Should never run"))
    .At(DateTime.UtcNow.AddSeconds(5))
    .Build();

// Task that will fail
scheduler.Schedule("t3", "Failing Task",
    () => throw new Exception("Boom!"))
    .At(DateTime.UtcNow.AddSeconds(1.5))
    .Build();

// Cancel t2 before it runs
Task.Delay(500).ContinueWith(_ => scheduler.CancelTask("t2"));

// Shutdown after 7 seconds
Task.Delay(7000).ContinueWith(_ => scheduler.Shutdown());

scheduler.Run(); // blocks until Shutdown() is called

Console.WriteLine("\n=== Shutdown complete ===");

// ─────────────────────────────────────────────────────────────────────────────

namespace V1_1
{
    // ── Enums ─────────────────────────────────────────────────────────────────

    enum TaskStatus  { Pending, Running, Completed, Failed, Cancelled }
    enum EventType   { Started, Completed, Failed, Cancelled }

    // ── Models ────────────────────────────────────────────────────────────────

    class ScheduledTask
    {
        public string     Id                 { get; }
        public string     Name               { get; }
        public Action     Action             { get; }
        public DateTime   ScheduledTime      { get; set; }   // mutable for recurring reschedule
        public TimeSpan?  RecurrenceInterval { get; }
        public bool       IsRecurring        => RecurrenceInterval.HasValue;

        public TaskStatus Status             { get; set; } = TaskStatus.Pending;

        public ScheduledTask(string id, string name, Action action,
            DateTime scheduledTime, TimeSpan? recurrenceInterval = null)
        {
            Id = id; Name = name; Action = action;
            ScheduledTime = scheduledTime;
            RecurrenceInterval = recurrenceInterval;
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
                EventType.Started   => ConsoleColor.Cyan,
                EventType.Completed => ConsoleColor.Green,
                EventType.Failed    => ConsoleColor.Red,
                EventType.Cancelled => ConsoleColor.Yellow,
                _                   => ConsoleColor.White
            };
            var msg = e.Exception != null ? $" — {e.Exception.Message}" : "";
            Console.WriteLine($"[{e.EventType,-10}] {e.TaskName}{msg}");
            Console.ResetColor();
        }
    }

    // ── Builder ───────────────────────────────────────────────────────────────

    class TaskBuilder(string id, string name, Action action, Scheduler scheduler)
    {
        private DateTime  _at       = DateTime.UtcNow;
        private TimeSpan? _interval = null;

        public TaskBuilder At(DateTime at)              { _at = at;             return this; }
        public TaskBuilder Recurring(TimeSpan interval) { _interval = interval; return this; }
        public void Build() => scheduler.Register(new ScheduledTask(id, name, action, _at, _interval));
    }

    // ── Scheduler ─────────────────────────────────────────────────────────────

    class Scheduler
    {
        private readonly Dictionary<string, ScheduledTask> _taskRepo = new();
        private readonly List<ITaskObserver> _observers = new();
        private bool _isCancelled = false;

        public void RegisterObserver(ITaskObserver o) => _observers.Add(o);

        public TaskBuilder Schedule(string id, string name, Action action)
            => new TaskBuilder(id, name, action, this);

        internal void Register(ScheduledTask task) => _taskRepo[task.Id] = task;

        public bool CancelTask(string id)
        {
            if (!_taskRepo.TryGetValue(id, out var task)) return false;
            if (task.Status != TaskStatus.Pending) return false;

            task.Status = TaskStatus.Cancelled;
            Notify(new TaskEvent(task.Id, task.Name, EventType.Cancelled, DateTime.UtcNow));
            return true;
        }

        // Single-threaded scheduler loop — blocks the calling thread
        public void Run()
        {
            while (_isCancelled == false)
            {
                var taskList = _taskRepo.Values.ToList();

                foreach (var task in taskList)
                {
                    if (task.Status != TaskStatus.Pending) continue;
                    if (DateTime.UtcNow < task.ScheduledTime) continue;

                    Execute(task);

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

        // ── Private ───────────────────────────────────────────────────────────

        private void Execute(ScheduledTask task)
        {
            task.Status = TaskStatus.Running;
            Notify(new TaskEvent(task.Id, task.Name, EventType.Started, DateTime.UtcNow));
            try
            {
                task.Action();
                task.Status = TaskStatus.Completed;
                Notify(new TaskEvent(task.Id, task.Name, EventType.Completed, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                task.Status = TaskStatus.Failed;
                Notify(new TaskEvent(task.Id, task.Name, EventType.Failed, DateTime.UtcNow, ex));
            }
        }

        private void Notify(TaskEvent e)
        {
            foreach (var o in _observers)
                try { o.OnEvent(e); } catch { /* observer errors must not crash scheduler */ }
        }
    }
}
