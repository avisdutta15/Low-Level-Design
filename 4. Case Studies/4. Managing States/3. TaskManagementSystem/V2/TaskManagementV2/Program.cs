using System.Collections.Concurrent;
using System.Collections.Immutable;

// Task Management System V2 — Thread-Safe
//
// V1 Gaps Fixed:
//   1. State transitions: per-task lock (check + set atomic, no TOCTOU)
//   2. Subtask completion guard: checked under lock with state change
//   3. Lists (subtasks, comments, logs): ImmutableList + ImmutableInterlocked
//   4. Observers: ImmutableList (safe add during notification)
//   5. Tags: ImmutableHashSet
//   6. Assignment/Priority: under per-task lock

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────
public enum TaskStatus { TODO, IN_PROGRESS, DONE }
public enum TaskPriority { LOW, MEDIUM, HIGH, CRITICAL }

// ─────────────────────────────────────────────
// User, Tag, Comment, ActivityLog (immutable — no thread concerns)
// ─────────────────────────────────────────────
public class User
{
    public string Id { get; }
    public string Name { get; }
    public string Email { get; }
    public User(string id, string name, string email) { Id = id; Name = name; Email = email; }
    public override string ToString() => Name;
}

public class Tag
{
    public string Name { get; }
    public Tag(string name) => Name = name;
    public override string ToString() => Name;
    public override int GetHashCode() => Name.GetHashCode();
    public override bool Equals(object? obj) => obj is Tag t && t.Name == Name;
}

public class Comment
{
    public string Id { get; }
    public string Content { get; }
    public User Author { get; }
    public DateTime Timestamp { get; }
    public Comment(User author, string content)
    { Id = Guid.NewGuid().ToString("N")[..8]; Author = author; Content = content; Timestamp = DateTime.Now; }
    public override string ToString() => $"{Author.Name}: \"{Content}\"";
}

public class ActivityLog
{
    public DateTime Timestamp { get; }
    public string Description { get; }
    public ActivityLog(string description) { Timestamp = DateTime.Now; Description = description; }
    public override string ToString() => $"[{Timestamp:HH:mm:ss}] {Description}";
}

// ─────────────────────────────────────────────
// TaskObserver
// ─────────────────────────────────────────────
public interface ITaskObserver
{
    void Update(Task task, string message);
}

public class ActivityLogger : ITaskObserver
{
    public void Update(Task task, string message)
    {
        Console.WriteLine($"    [Log] Task \"{task.Title}\": {message}");
    }
}

// ─────────────────────────────────────────────
// Task — per-task lock, ImmutableLists, atomic transitions
// ─────────────────────────────────────────────

// V2: State logic is INLINED in Task's transition methods under the per-task lock.
// Why not separate State classes? In V1, state classes called task.SetState() which
// was unprotected. With a per-task lock, the check+transition must happen atomically
// INSIDE the lock. Delegating to an external state class that also needs the lock
// creates complexity (lock re-entrancy, passing lock references). Simpler: keep
// the state machine logic inside Task's locked methods with a TaskStatus enum.
public class Task
{
    public string Id { get; }
    public string Title { get; set; }
    public string Description { get; set; }
    public User CreatedBy { get; }

    private readonly object _lock = new(); // Per-task lock for all mutations

    // V2: All mutable state protected by _lock or Interlocked/Immutable
    private TaskStatus _status;
    private TaskPriority _priority;
    private DateTime _dueDate;
    private User? _assignee;

    // V2: ImmutableList for safe concurrent iteration + modification
    private ImmutableList<Task> _subtasks = ImmutableList<Task>.Empty;
    private ImmutableList<ActivityLog> _activityLogs = ImmutableList<ActivityLog>.Empty;
    private ImmutableList<Comment> _comments = ImmutableList<Comment>.Empty;
    private ImmutableHashSet<Tag> _tags = ImmutableHashSet<Tag>.Empty;
    private ImmutableList<ITaskObserver> _observers = ImmutableList<ITaskObserver>.Empty;

    public Task(string title, string description, User createdBy, DateTime dueDate, TaskPriority priority)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Title = title; Description = description; CreatedBy = createdBy;
        _dueDate = dueDate; _priority = priority; _status = TaskStatus.TODO;
        AddLog($"Task created by {createdBy.Name}");
    }

    // ── Thread-safe reads ──
    public TaskStatus GetStatus() { lock (_lock) { return _status; } }
    public TaskPriority Priority { get { lock (_lock) { return _priority; } } }
    public DateTime DueDate { get { lock (_lock) { return _dueDate; } } set { lock (_lock) { _dueDate = value; } } }
    public User? Assignee { get { lock (_lock) { return _assignee; } } }
    public ImmutableList<Task> Subtasks => _subtasks;
    public ImmutableList<ActivityLog> ActivityLogs => _activityLogs;
    public ImmutableList<Comment> Comments => _comments;
    public ImmutableHashSet<Tag> Tags => _tags;

    // ── State Transitions (atomic under per-task lock) ──

    // TODO → IN_PROGRESS
    public bool StartProgress()
    {
        lock (_lock)
        {
            if (_status != TaskStatus.TODO)
            {
                Console.WriteLine($"    [Error] Cannot start \"{Title}\" — status is {_status}");
                return false;
            }
            _status = TaskStatus.IN_PROGRESS;
        }
        AddLog("Status: TODO → IN_PROGRESS");
        NotifyObservers("Status changed to IN_PROGRESS");
        return true;
    }

    // IN_PROGRESS → DONE (only if all subtasks are DONE)
    public bool CompleteTask()
    {
        lock (_lock)
        {
            if (_status != TaskStatus.IN_PROGRESS)
            {
                Console.WriteLine($"    [Error] Cannot complete \"{Title}\" — status is {_status} (must be IN_PROGRESS)");
                return false;
            }
            // Subtask guard: all must be DONE (checked under lock)
            if (_subtasks.Any(s => s.GetStatus() != TaskStatus.DONE))
            {
                Console.WriteLine($"    [Error] Cannot complete \"{Title}\" — subtasks not all done");
                return false;
            }
            _status = TaskStatus.DONE;
        }
        AddLog("Status: IN_PROGRESS → DONE");
        NotifyObservers("Status changed to DONE");
        return true;
    }

    // DONE or IN_PROGRESS → TODO
    public bool ReopenTask()
    {
        lock (_lock)
        {
            if (_status == TaskStatus.TODO)
            {
                Console.WriteLine($"    [Error] \"{Title}\" is already TODO");
                return false;
            }
            var old = _status;
            _status = TaskStatus.TODO;
            AddLogInternal($"Status: {old} → TODO (reopened)");
        }
        NotifyObservers("Reopened to TODO");
        return true;
    }

    // ── Assignment (under lock) ──
    public void Assign(User user)
    {
        string prev;
        lock (_lock)
        {
            prev = _assignee?.Name ?? "unassigned";
            _assignee = user;
        }
        AddLog($"Assigned: {prev} → {user.Name}");
        NotifyObservers($"Assigned to {user.Name}");
    }

    // ── Priority (under lock) ──
    public void UpdatePriority(TaskPriority newPriority)
    {
        TaskPriority old;
        lock (_lock) { old = _priority; _priority = newPriority; }
        AddLog($"Priority: {old} → {newPriority}");
        NotifyObservers($"Priority changed to {newPriority}");
    }

    // ── Subtasks (ImmutableList — safe to add while iterating) ──
    public bool IsComposite() => _subtasks.Count > 0;

    public void AddSubtask(Task subtask)
    {
        ImmutableInterlocked.Update(ref _subtasks, list => list.Add(subtask));
        AddLog($"Subtask added: \"{subtask.Title}\"");
        NotifyObservers($"Subtask added: \"{subtask.Title}\"");
    }

    // ── Tags (ImmutableHashSet) ──
    public void AddTag(Tag tag) => ImmutableInterlocked.Update(ref _tags, set => set.Add(tag));

    // ── Comments (ImmutableList) ──
    public void AddComment(Comment comment)
    {
        ImmutableInterlocked.Update(ref _comments, list => list.Add(comment));
        AddLog($"Comment by {comment.Author.Name}");
    }

    // ── Activity Log (ImmutableList) ──
    public void AddLog(string description) =>
        ImmutableInterlocked.Update(ref _activityLogs, list => list.Add(new ActivityLog(description)));

    private void AddLogInternal(string description) =>
        ImmutableInterlocked.Update(ref _activityLogs, list => list.Add(new ActivityLog(description)));

    // ── Observers (ImmutableList — safe add during notification) ──
    public void AddObserver(ITaskObserver observer) =>
        ImmutableInterlocked.Update(ref _observers, list => list.Add(observer));

    public void NotifyObservers(string message)
    {
        var snapshot = _observers; // immutable snapshot
        foreach (var obs in snapshot) obs.Update(this, message);
    }

    // ── Display ──
    public void Display(string indent = "")
    {
        Console.WriteLine($"{indent}{this}");
        foreach (var sub in _subtasks)
            sub.Display(indent + "  ");
    }

    public override string ToString() =>
        $"[{GetStatus()}] \"{Title}\" (P:{Priority}, Due:{DueDate:dd-MMM}" +
        (Assignee != null ? $", @{Assignee.Name}" : "") +
        (IsComposite() ? $", subtasks:{_subtasks.Count}" : "") + ")";
}

// ─────────────────────────────────────────────
// TaskList, Sort Strategies (same as V1)
// ─────────────────────────────────────────────
public class TaskList
{
    public string Id { get; }
    public string Name { get; }
    private ImmutableList<Task> _tasks = ImmutableList<Task>.Empty;

    public TaskList(string name) { Id = Guid.NewGuid().ToString("N")[..8]; Name = name; }
    public void AddTask(Task task) => ImmutableInterlocked.Update(ref _tasks, list => list.Add(task));
    public ImmutableList<Task> Tasks => _tasks;

    public void Display()
    {
        Console.WriteLine($"    === {Name} ({_tasks.Count} tasks) ===");
        foreach (var task in _tasks) task.Display("      ");
    }
}

public interface ITaskSortStrategy { void Sort(List<Task> tasks); }
public class SortByDueDate : ITaskSortStrategy
{
    public void Sort(List<Task> tasks) => tasks.Sort((a, b) => a.DueDate.CompareTo(b.DueDate));
}
public class SortByPriority : ITaskSortStrategy
{
    public void Sort(List<Task> tasks) => tasks.Sort((a, b) => b.Priority.CompareTo(a.Priority));
}

// ─────────────────────────────────────────────
// TaskManagementSystem (Singleton Facade)
// ─────────────────────────────────────────────
public class TaskManagementSystem
{
    private static TaskManagementSystem? _instance;
    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly ConcurrentDictionary<string, Task> _tasks = new();
    private readonly ConcurrentDictionary<string, TaskList> _taskLists = new();
    private readonly ITaskObserver _logger = new ActivityLogger();

    private TaskManagementSystem() { }
    public static TaskManagementSystem GetInstance() { _instance ??= new TaskManagementSystem(); return _instance; }

    public User CreateUser(string name, string email)
    {
        var user = new User(Guid.NewGuid().ToString("N")[..8], name, email);
        _users.TryAdd(user.Id, user);
        return user;
    }

    public TaskList CreateTaskList(string name)
    {
        var list = new TaskList(name);
        _taskLists.TryAdd(list.Id, list);
        return list;
    }

    public Task CreateTask(string title, string description, DateTime dueDate, TaskPriority priority, User createdBy)
    {
        var task = new Task(title, description, createdBy, dueDate, priority);
        task.AddObserver(_logger);
        _tasks.TryAdd(task.Id, task);
        Console.WriteLine($"    [Created] {task}");
        return task;
    }

    public void DeleteTask(string taskId)
    {
        if (_tasks.TryRemove(taskId, out var task))
            Console.WriteLine($"    [Deleted] \"{task.Title}\"");
    }

    public List<Task> ListTasksByUser(string userId) =>
        _tasks.Values.Where(t => t.Assignee?.Id == userId).ToList();

    public List<Task> ListTasksByStatus(TaskStatus status) =>
        _tasks.Values.Where(t => t.GetStatus() == status).ToList();

    public List<Task> SearchTasks(string keyword, ITaskSortStrategy? sort = null)
    {
        var results = _tasks.Values
            .Where(t => t.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        t.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        t.Tags.Any(tag => tag.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (sort != null) sort.Sort(results);
        return results;
    }
}

// ─────────────────────────────────────────────
// Demo — concurrent transitions
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var system = TaskManagementSystem.GetInstance();

        var alice = system.CreateUser("Alice", "alice@mail.com");
        var bob = system.CreateUser("Bob", "bob@mail.com");

        // ── Create tasks ──
        Console.WriteLine("=== Create Tasks ===\n");
        var task1 = system.CreateTask("Build login", "OAuth implementation",
            new DateTime(2025, 8, 1), TaskPriority.HIGH, alice);
        var task2 = system.CreateTask("Write tests", "Cover auth",
            new DateTime(2025, 8, 5), TaskPriority.MEDIUM, alice);

        task1.Assign(alice);
        task2.Assign(bob);

        // ── Subtasks ──
        Console.WriteLine("\n=== Subtasks ===\n");
        var sub1 = system.CreateTask("Google OAuth", "SSO",
            new DateTime(2025, 7, 30), TaskPriority.HIGH, alice);
        var sub2 = system.CreateTask("GitHub OAuth", "SSO",
            new DateTime(2025, 7, 31), TaskPriority.MEDIUM, alice);
        task1.AddSubtask(sub1);
        task1.AddSubtask(sub2);

        // ── Scenario 1: Concurrent StartProgress on same task ──
        Console.WriteLine("\n=== Scenario 1: Concurrent StartProgress (only one should succeed from TODO) ===\n");

        bool result1 = false, result2 = false;
        System.Threading.Tasks.Task.WaitAll(
            System.Threading.Tasks.Task.Run(() => { result1 = task2.StartProgress(); }),
            System.Threading.Tasks.Task.Run(() => { result2 = task2.StartProgress(); }));

        Console.WriteLine($"    Thread 1: {(result1 ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"    Thread 2: {(result2 ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"    (Exactly one should succeed — per-task lock)");
        Console.WriteLine($"    Status: {task2.GetStatus()}");

        // ── Scenario 2: Complete with subtask guard ──
        Console.WriteLine("\n=== Scenario 2: Complete with subtask guard ===\n");
        task1.StartProgress();
        task1.CompleteTask(); // FAIL: subtasks not done

        sub1.StartProgress(); sub1.CompleteTask();
        sub2.StartProgress(); sub2.CompleteTask();
        task1.CompleteTask(); // NOW succeeds

        // ── Scenario 3: Concurrent complete + reopen race ──
        Console.WriteLine("\n=== Scenario 3: Concurrent Complete vs Reopen ===\n");

        var task3 = system.CreateTask("Quick task", "Test",
            new DateTime(2025, 8, 1), TaskPriority.LOW, bob);
        task3.StartProgress();

        bool completeOk = false, reopenOk = false;
        System.Threading.Tasks.Task.WaitAll(
            System.Threading.Tasks.Task.Run(() => { completeOk = task3.CompleteTask(); }),
            System.Threading.Tasks.Task.Run(() => { reopenOk = task3.ReopenTask(); }));

        Console.WriteLine($"    Complete: {(completeOk ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"    Reopen:   {(reopenOk ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"    Final: {task3.GetStatus()} (per-task lock ensures one wins)");

        // ── Scenario 4: Normal lifecycle ──
        Console.WriteLine("\n=== Scenario 4: Full lifecycle ===\n");
        task2.CompleteTask();
        task1.AddComment(new Comment(bob, "Looks good!"));
        task1.UpdatePriority(TaskPriority.CRITICAL);
        task1.AddTag(new Tag("auth"));

        // ── Display ──
        Console.WriteLine("\n=== Activity Log: task1 ===\n");
        foreach (var log in task1.ActivityLogs)
            Console.WriteLine($"    {log}");
    }
}
