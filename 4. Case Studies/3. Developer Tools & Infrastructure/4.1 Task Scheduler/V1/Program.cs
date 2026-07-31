// ── Demo ─────────────────────────────────────────────────────────────────────
// V1 — Single-threaded Task Scheduler
// Features: one-time tasks, recurring tasks, cancel, observer notifications
// The two Task.Delay(...).ContinueWith(...) calls in Program.cs (for cancel and shutdown)
// do run on threadpool threads, but they only mutate a flag (isCancelled) or call CancelTask()
// which just flips a status — the actual task execution never leaves the main thread.
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using System.Collections.Concurrent;
using V1;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.ComponentModel.DataAnnotations;

var scheduler = new Scheduler();
scheduler.RegisterObserver(new ConsoleObserver());

Console.WriteLine("=== V1: Single-threaded Scheduler ===\n");

// One-time task: runs after 1 second
scheduler.ScheduleTask("t1", "One-Time Report", 
    () =>
{
    Console.WriteLine("  → Generating report...");
    Thread.Sleep(200);
}, DateTime.UtcNow.AddSeconds(1));

// Recurring task: runs every 2 seconds
scheduler.ScheduleRecurring("hb", "Heartbeat",
    () => Console.WriteLine("  → ♥ ping"), TimeSpan.FromSeconds(2));

// Task that will be cancelled before it runs
scheduler.ScheduleTask("t2", "Cancelled Task",
    () => Console.WriteLine("  → Should never run"), DateTime.UtcNow.AddSeconds(5));

// Task that will fail
scheduler.ScheduleTask("t3", "Failing Task",
    () => throw new Exception("Boom!"), DateTime.UtcNow.AddSeconds(1.5));

// Cancel t2 before it runs
Task.Delay(500).ContinueWith(_ => scheduler.CancelTask("t2"));

// Shutdown after 7 seconds
Task.Delay(7000).ContinueWith(_ => scheduler.Shutdown());

scheduler.Run(); // blocks until Shutdown() is called

Console.WriteLine("\n=== Shutdown complete ===");

// ─────────────────────────────────────────────────────────────────────────────

namespace V1
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

        // All tasks are by default in pending state
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

    // ── Scheduler ─────────────────────────────────────────────────────────────

    class Scheduler
    {
        private readonly Dictionary<string, ScheduledTask> _taskRepo = new();
        private readonly List<ITaskObserver> _observers = new();
        private bool _isCancelled = false;

        public void RegisterObserver(ITaskObserver o)
        {
            _observers.Add(o);
        }

        // Schedule a one-time task at a future time
        public void ScheduleTask(string id, string name, Action action, DateTime at)
        {
            // Add this task to repo
            _taskRepo[id] = new ScheduledTask(id, name, action, at); 
        }

        // Schedule a recurring task starting now, repeating every interval
        public void ScheduleRecurring(string id, string name, Action action, TimeSpan interval)
        {
            // Add this task to repo
            _taskRepo[id] = new ScheduledTask(id, name, action, DateTime.UtcNow, interval);
        }

        public bool CancelTask(string id)
        {
            if (!_taskRepo.TryGetValue(id, out var task)) return false;
            if (task.Status != TaskStatus.Pending) return false;

            task.Status = TaskStatus.Cancelled;
            Notify(new TaskEvent(task.Id, task.Name, EventType.Cancelled, DateTime.UtcNow));
            return true;
        }

        // Single-threaded scheduler loop — blocks the calling thread
        // After every iteration, it sleeps for 100ms to avoid busy-waiting.
        public void Run()
        {
            while (_isCancelled == false)
            {
                var taskList = _taskRepo.Values.ToList();
                
                // Iterate over the list of tasks
                foreach (var task in taskList)
                {
                    // Task already scheduled or inprogress or completed.
                    if (task.Status != TaskStatus.Pending) continue;

                    // Check if it is ready to be executed.
                    if (DateTime.UtcNow < task.ScheduledTime) continue;

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

        public void Shutdown()
        {
            _isCancelled = true;
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
            }
            catch (Exception ex)
            {
                // Running -> Failed
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



public class CacheEntry
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } =  string.Empty;
    public DateTime ExpiryTime { get; set; }
}

public class Cache
{
    private readonly int _capacity;
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cacheMap = new();
    private readonly LinkedList<CacheEntry> _lruList = new();
    // The lock object ensures thread safety across both collections
    private readonly object _lock = new object();
    private Timer _janitorTimer;     // Timer to run the background cleanup task

    public Cache(int capacity, TimeSpan cleanupInterval)
    {
        _capacity = capacity;
        _janitorTimer = new Timer(Cleanup, null, cleanupInterval, cleanupInterval);
    }

    private void Cleanup(object? state)
    {
        LinkedListNode<CacheEntry> current = _lruList.First;
        lock (_lock) 
        { 
            while(current != null)
            {
                CacheEntry entry = current.Value;
                // Grab next before potentially deleting current
                var next = current.Next;
                if (entry.ExpiryTime < DateTime.UtcNow)
                {
                    RemoveNode(current);
                }
                current = next;
            }
        }
    }

    // Retrieves the value associated with the key
    public string Get(string key)
    {
        lock (_lock) {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                // Check if expired
                if (DateTime.UtcNow > node.Value.ExpiryTime)
                {
                    // Remove the expired entry from hash map and list
                    RemoveNode(node);
                    throw new KeyNotFoundException($"The key '{key}' has expired.");
                }

                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.Value;
            }
            return string.Empty;
        }
    }

    // Inserts the value with an optional TTL (Time-To-Live)
    public void Set(string key, string value, TimeSpan? ttl = null)
    {
        DateTime expiry = ttl.HasValue
            ? DateTime.UtcNow.Add(ttl.Value)
            : DateTime.MaxValue;
        lock (_lock) 
        {
            // Check if key exists
            if (_cacheMap.TryGetValue(key, out var existingNode))
            {
                // Update existing item
                existingNode.Value.Value = value;
                existingNode.Value.ExpiryTime = expiry;

                // Move to front
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
            }
            else
            {
                // if doesnot exists
                var newEntry = new CacheEntry { Key = key, Value = value, ExpiryTime = expiry };
                var newNode = new LinkedListNode<CacheEntry>(newEntry);

                _cacheMap[key] = newNode;
                _lruList.AddFirst(newNode);

                // LRU if capacity exceeds
                if (_cacheMap.Count > _capacity)
                {
                    RemoveNode(_lruList.Last);
                }                
            }
        }
    }

    // Removes the key from the cache
    public void Remove(string key)
    {
        lock (_lock)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                RemoveNode(node);
            }
        }
    }

    private void RemoveNode(LinkedListNode<CacheEntry> node)
    {
        _cacheMap.Remove(node.Value.Key);
        _lruList.Remove(node);
    }
}