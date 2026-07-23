// ── Demo ─────────────────────────────────────────────────────────────────────
// V1 — Single-threaded Task Scheduler
// Features: one-time tasks, recurring tasks, cancel, observer notifications

using V1;

// Create the scheduler and attach a console observer for lifecycle logging
var scheduler = new Scheduler();
scheduler.Subscribe(new ConsoleObserver());

Console.WriteLine("=== V1: Single-threaded Scheduler ===\n");

// One-time task: runs after 1 second
scheduler.ScheduleTask("t1", "One-Time Report", () =>
{
    Console.WriteLine("  → Generating report...");
    Thread.Sleep(200);
}, DateTime.UtcNow.AddSeconds(1));

// Recurring task: runs every 2 seconds, starting immediately
scheduler.ScheduleRecurring("hb", "Heartbeat",
    () => Console.WriteLine("  → ♥ ping"), TimeSpan.FromSeconds(2));

// Task that will be cancelled before it runs (scheduled far in future)
scheduler.ScheduleTask("t2", "Cancelled Task",
    () => Console.WriteLine("  → Should never run"), DateTime.UtcNow.AddSeconds(5));

// Task that will fail with an exception
scheduler.ScheduleTask("t3", "Failing Task",
    () => throw new Exception("Boom!"), DateTime.UtcNow.AddSeconds(1.5));

// Cancel t2 after 500ms — before its scheduled time of 5s
Task.Delay(500).ContinueWith(_ => scheduler.CancelTask("t2"));

// Shutdown after 7 seconds — exits the Run() loop
Task.Delay(7000).ContinueWith(_ => scheduler.Shutdown());

// Run() blocks the main thread, polling tasks every 100ms
scheduler.Run();

Console.WriteLine("\n=== Shutdown complete ===");

// ─────────────────────────────────────────────────────────────────────────────

namespace V1
{
    // ── Enums ─────────────────────────────────────────────────────────────────

    // Lifecycle states a task can be in
    enum TaskStatus  { Pending, Running, Completed, Failed, Cancelled }

    // Types of events observers get notified about
    enum EventType   { Started, Completed, Failed, Cancelled }

    // ── Models ────────────────────────────────────────────────────────────────

    // Represents a unit of work with scheduling metadata
    class ScheduledTask
    {
        public string     Id                 { get; }            // Unique identifier
        public string     Name               { get; }            // Human-readable name for logging
        public Action     Action             { get; }            // The delegate to execute
        public DateTime   ScheduledTime      { get; set; }       // Earliest UTC time to run (mutable for recurring reschedule)
        public TimeSpan?  RecurrenceInterval { get; }            // If set, task repeats at this interval
        public bool       IsRecurring        => RecurrenceInterval.HasValue;  // Convenience check
        public TaskStatus Status             { get; set; } = TaskStatus.Pending;  // Current lifecycle state

        public ScheduledTask(string id, string name, Action action,
            DateTime scheduledTime, TimeSpan? recurrenceInterval = null)
        {
            Id = id; Name = name; Action = action;
            ScheduledTime = scheduledTime;
            RecurrenceInterval = recurrenceInterval;
        }
    }

    // Immutable event record passed to observers on each lifecycle transition
    record TaskEvent(string TaskId, string TaskName, EventType EventType,
        DateTime Timestamp, Exception? Exception = null);

    // ── Observer ──────────────────────────────────────────────────────────────

    // Contract for receiving task lifecycle notifications (Observer pattern)
    interface ITaskObserver { void OnEvent(TaskEvent e); }

    // Concrete observer: prints color-coded events to console
    class ConsoleObserver : ITaskObserver
    {
        public void OnEvent(TaskEvent e)
        {
            // Color-code by event type for quick visual scanning
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

    // ── Scheduler ─────────────────────────────────────────────────────────────

    // Orchestrates task registration, polling loop, execution, and observer notifications
    class Scheduler
    {
        // Task registry keyed by ID for O(1) lookup
        private readonly Dictionary<string, ScheduledTask> _tasks = new();
        // Registered lifecycle observers
        private readonly List<ITaskObserver> _observers = new();
        // Flag to control the polling loop; set to false by Shutdown()
        private bool _running = true;

        // Register an observer to receive lifecycle events
        public void Subscribe(ITaskObserver o) => _observers.Add(o);

        // Add a one-time task to the registry, scheduled at a specific time
        public void ScheduleTask(string id, string name, Action action, DateTime at)
            => _tasks[id] = new ScheduledTask(id, name, action, at);

        // Add a recurring task starting immediately, repeating every `interval`
        public void ScheduleRecurring(string id, string name, Action action, TimeSpan interval)
            => _tasks[id] = new ScheduledTask(id, name, action, DateTime.UtcNow, interval);

        // Cancel a pending task; returns false if not found or already past Pending state
        public bool CancelTask(string id)
        {
            if (!_tasks.TryGetValue(id, out var task)) return false;   // Task doesn't exist
            if (task.Status != TaskStatus.Pending) return false;        // Can only cancel Pending tasks

            task.Status = TaskStatus.Cancelled;
            Notify(new TaskEvent(task.Id, task.Name, EventType.Cancelled, DateTime.UtcNow));
            return true;
        }

        // Single-threaded scheduler loop — blocks the calling thread until Shutdown()
        // Polls every 100ms checking if any task is ready to execute
        public void Run()
        {
            while (_running)
            {
                // Snapshot with ToList() to allow modification during iteration
                foreach (var task in _tasks.Values.ToList())
                {
                    // Skip tasks that are not in Pending state (already ran, failed, or cancelled)
                    if (task.Status != TaskStatus.Pending) continue;

                    // Skip tasks whose scheduled time hasn't arrived yet
                    if (DateTime.UtcNow < task.ScheduledTime) continue;

                    // Time is up and task is pending — execute it synchronously
                    Execute(task);

                    // If recurring and completed successfully, reschedule for the next interval
                    if (task.IsRecurring && task.Status == TaskStatus.Completed)
                    {
                        task.ScheduledTime = DateTime.UtcNow.Add(task.RecurrenceInterval!.Value);
                        task.Status = TaskStatus.Pending;  // Reset to Pending for next cycle
                    }
                }

                // 100ms polling interval — balance between responsiveness and CPU usage
                Thread.Sleep(100);
            }
        }

        // Signal the polling loop to exit
        public void Shutdown() => _running = false;

        // ── Private ───────────────────────────────────────────────────────────

        // Execute the task's action, transition status, and notify observers
        private void Execute(ScheduledTask task)
        {
            // Transition: Pending → Running
            task.Status = TaskStatus.Running;
            Notify(new TaskEvent(task.Id, task.Name, EventType.Started, DateTime.UtcNow));
            try
            {
                task.Action();  // Run the actual work
                // Transition: Running → Completed
                task.Status = TaskStatus.Completed;
                Notify(new TaskEvent(task.Id, task.Name, EventType.Completed, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                // Transition: Running → Failed
                task.Status = TaskStatus.Failed;
                Notify(new TaskEvent(task.Id, task.Name, EventType.Failed, DateTime.UtcNow, ex));
            }
        }

        // Broadcast event to all observers; swallow observer exceptions to prevent crashes
        private void Notify(TaskEvent e)
        {
            foreach (var o in _observers)
                try { o.OnEvent(e); } catch { /* observer errors must not crash scheduler */ }
        }
    }
}
