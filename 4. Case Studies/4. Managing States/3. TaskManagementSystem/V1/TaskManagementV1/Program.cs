using System.Collections.Concurrent;

// Task Management System V1
//
// Design Patterns:
//   - State Pattern: TaskState (TodoState, InProgressState, DoneState) controls valid transitions
//   - Observer Pattern: TaskObserver notified on task changes (ActivityLogger)
//   - Strategy Pattern: TaskSortStrategy for sorting task lists (ByDueDate, ByPriority)
//   - Composite: Tasks can have subtasks; parent completes only when all subtasks are done
//
// Entities (from class diagram):
//   User, Task, TaskList, Tag, Comment, ActivityLog
//   TaskState (interface) + TodoState, InProgressState, DoneState
//   TaskObserver (interface) + ActivityLogger
//   TaskSortStrategy (interface) + SortByDueDate, SortByPriority
//   TaskManagementSystem (Singleton facade)

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────
public enum TaskStatus { TODO, IN_PROGRESS, DONE }
public enum TaskPriority { LOW, MEDIUM, HIGH, CRITICAL }

// ─────────────────────────────────────────────
// User, Tag, Comment, ActivityLog
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
    {
        Id = Guid.NewGuid().ToString("N")[..8]; Author = author; Content = content; Timestamp = DateTime.Now;
    }
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
// TaskObserver (Observer Pattern)
// ─────────────────────────────────────────────
public interface ITaskObserver
{
    void Update(Task task, string message);
}

// Logs all task changes to console
public class ActivityLogger : ITaskObserver
{
    public void Update(Task task, string message)
    {
        Console.WriteLine($"    [Log] Task \"{task.Title}\": {message}");
    }
}

// ─────────────────────────────────────────────
// TaskState (State Pattern) — controls valid status transitions
// ─────────────────────────────────────────────

// Each state defines what transitions are valid FROM that state.
// Invalid transitions print an error and do nothing.
public interface ITaskState
{
    void StartProgress(Task task);
    void CompleteTask(Task task);
    void ReopenTask(Task task);
    TaskStatus GetStatus();
}

// TodoState: can start progress, CANNOT complete directly (must go through InProgress)
public class TodoState : ITaskState
{
    public void StartProgress(Task task)
    {
        task.SetState(new InProgressState());
        task.AddLog("Status: TODO → IN_PROGRESS");
        task.NotifyObservers("Status changed to IN_PROGRESS");
    }

    public void CompleteTask(Task task)
    {
        Console.WriteLine($"    [Error] Cannot complete \"{task.Title}\" directly from TODO. Start progress first.");
    }

    public void ReopenTask(Task task)
    {
        Console.WriteLine($"    [Error] \"{task.Title}\" is already in TODO.");
    }

    public TaskStatus GetStatus() => TaskStatus.TODO;
}

// InProgressState: can complete or reopen (back to TODO)
public class InProgressState : ITaskState
{
    public void StartProgress(Task task)
    {
        Console.WriteLine($"    [Error] \"{task.Title}\" is already in progress.");
    }

    public void CompleteTask(Task task)
    {
        // Check subtasks: parent can only complete if ALL subtasks are done
        if (task.IsComposite() && task.Subtasks.Any(s => s.GetStatus() != TaskStatus.DONE))
        {
            Console.WriteLine($"    [Error] Cannot complete \"{task.Title}\" — subtasks not all done.");
            return;
        }

        task.SetState(new DoneState());
        task.AddLog("Status: IN_PROGRESS → DONE");
        task.NotifyObservers("Status changed to DONE");
    }

    public void ReopenTask(Task task)
    {
        task.SetState(new TodoState());
        task.AddLog("Status: IN_PROGRESS → TODO (reopened)");
        task.NotifyObservers("Reopened to TODO");
    }

    public TaskStatus GetStatus() => TaskStatus.IN_PROGRESS;
}

// DoneState: can reopen, cannot start progress or complete again
public class DoneState : ITaskState
{
    public void StartProgress(Task task)
    {
        Console.WriteLine($"    [Error] \"{task.Title}\" is done. Reopen first.");
    }

    public void CompleteTask(Task task)
    {
        Console.WriteLine($"    [Error] \"{task.Title}\" is already done.");
    }

    public void ReopenTask(Task task)
    {
        task.SetState(new TodoState());
        task.AddLog("Status: DONE → TODO (reopened)");
        task.NotifyObservers("Reopened from DONE to TODO");
    }

    public TaskStatus GetStatus() => TaskStatus.DONE;
}

// ─────────────────────────────────────────────
// Task (Composite: can have subtasks)
// ─────────────────────────────────────────────
public class Task
{
    public string Id { get; }
    public string Title { get; set; }
    public string Description { get; set; }
    public DateTime DueDate { get; set; }
    public TaskPriority Priority { get; private set; }
    public User CreatedBy { get; }
    public User? Assignee { get; private set; }

    private ITaskState _currentState;
    private readonly List<Task> _subtasks = new();
    private readonly List<ActivityLog> _activityLogs = new();
    private readonly HashSet<Tag> _tags = new();
    private readonly List<Comment> _comments = new();
    private readonly List<ITaskObserver> _observers = new();

    public Task(string title, string description, User createdBy, DateTime dueDate, TaskPriority priority)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Title = title; Description = description;
        CreatedBy = createdBy; DueDate = dueDate; Priority = priority;
        _currentState = new TodoState();
        AddLog($"Task created by {createdBy.Name}");
    }

    // ── State Pattern delegates ──
    public void StartProgress() => _currentState.StartProgress(this);
    public void CompleteTask() => _currentState.CompleteTask(this);
    public void ReopenTask() => _currentState.ReopenTask(this);
    public TaskStatus GetStatus() => _currentState.GetStatus();
    public void SetState(ITaskState state) => _currentState = state;

    // ── Subtasks (Composite) ──
    public IReadOnlyList<Task> Subtasks => _subtasks.AsReadOnly();
    public bool IsComposite() => _subtasks.Count > 0;

    public void AddSubtask(Task subtask)
    {
        _subtasks.Add(subtask);
        AddLog($"Subtask added: \"{subtask.Title}\"");
        NotifyObservers($"Subtask added: \"{subtask.Title}\"");
    }

    // ── Assignment ──
    public void Assign(User user)
    {
        var prev = Assignee?.Name ?? "unassigned";
        Assignee = user;
        AddLog($"Assigned: {prev} → {user.Name}");
        NotifyObservers($"Assigned to {user.Name}");
    }

    // ── Priority ──
    public void UpdatePriority(TaskPriority newPriority)
    {
        var old = Priority;
        Priority = newPriority;
        AddLog($"Priority: {old} → {newPriority}");
        NotifyObservers($"Priority changed to {newPriority}");
    }

    // ── Tags ──
    public IReadOnlyCollection<Tag> Tags => _tags;
    public void AddTag(Tag tag) => _tags.Add(tag);

    // ── Comments ──
    public IReadOnlyList<Comment> Comments => _comments.AsReadOnly();
    public void AddComment(Comment comment)
    {
        _comments.Add(comment);
        AddLog($"Comment by {comment.Author.Name}");
    }

    // ── Activity Log ──
    public IReadOnlyList<ActivityLog> ActivityLogs => _activityLogs.AsReadOnly();
    public void AddLog(string description) => _activityLogs.Add(new ActivityLog(description));

    // ── Observer ──
    public void AddObserver(ITaskObserver observer) => _observers.Add(observer);
    public void RemoveObserver(ITaskObserver observer) => _observers.Remove(observer);
    public void NotifyObservers(string message)
    {
        foreach (var obs in _observers) obs.Update(this, message);
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
// TaskList
// ─────────────────────────────────────────────
public class TaskList
{
    public string Id { get; }
    public string Name { get; }
    private readonly List<Task> _tasks = new();

    public TaskList(string name) { Id = Guid.NewGuid().ToString("N")[..8]; Name = name; }

    public void AddTask(Task task) => _tasks.Add(task);
    public IReadOnlyList<Task> Tasks => _tasks.AsReadOnly();

    public void Display()
    {
        Console.WriteLine($"    === {Name} ({_tasks.Count} tasks) ===");
        foreach (var task in _tasks)
            task.Display("      ");
    }
}

// ─────────────────────────────────────────────
// TaskSortStrategy (Strategy Pattern)
// ─────────────────────────────────────────────
public interface ITaskSortStrategy
{
    void Sort(List<Task> tasks);
}

public class SortByDueDate : ITaskSortStrategy
{
    public void Sort(List<Task> tasks) => tasks.Sort((a, b) => a.DueDate.CompareTo(b.DueDate));
}

public class SortByPriority : ITaskSortStrategy
{
    public void Sort(List<Task> tasks) => tasks.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // CRITICAL first
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

    public static TaskManagementSystem GetInstance()
    {
        _instance ??= new TaskManagementSystem();
        return _instance;
    }

    // ── Users ──

    public User CreateUser(string name, string email)
    {
        var user = new User(Guid.NewGuid().ToString("N")[..8], name, email);
        _users.TryAdd(user.Id, user);
        return user;
    }

    // ── Task Lists ──

    public TaskList CreateTaskList(string name)
    {
        var list = new TaskList(name);
        _taskLists.TryAdd(list.Id, list);
        return list;
    }

    // ── Tasks ──

    public Task CreateTask(string title, string description, DateTime dueDate,
        TaskPriority priority, User createdBy)
    {
        var task = new Task(title, description, createdBy, dueDate, priority);
        task.AddObserver(_logger); // auto-attach logger
        _tasks.TryAdd(task.Id, task);
        Console.WriteLine($"    [Created] {task}");
        return task;
    }

    public void DeleteTask(string taskId)
    {
        if (_tasks.TryRemove(taskId, out var task))
            Console.WriteLine($"    [Deleted] \"{task.Title}\"");
    }

    // ── Filtering ──

    public List<Task> ListTasksByUser(string userId)
    {
        return _tasks.Values.Where(t => t.Assignee?.Id == userId).ToList();
    }

    public List<Task> ListTasksByStatus(TaskStatus status)
    {
        return _tasks.Values.Where(t => t.GetStatus() == status).ToList();
    }

    public List<Task> SearchTasks(string keyword, ITaskSortStrategy? sortStrategy = null)
    {
        var results = _tasks.Values
            .Where(t => t.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        t.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        t.Tags.Any(tag => tag.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (sortStrategy != null) sortStrategy.Sort(results);
        return results;
    }
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var system = TaskManagementSystem.GetInstance();

        // ── Create users ──
        var alice = system.CreateUser("Alice", "alice@mail.com");
        var bob = system.CreateUser("Bob", "bob@mail.com");

        // ── Create task list ──
        var sprintList = system.CreateTaskList("Sprint 1");

        // ── Create tasks ──
        Console.WriteLine("=== Create Tasks ===\n");
        var task1 = system.CreateTask("Build login page", "Implement OAuth",
            new DateTime(2025, 8, 1), TaskPriority.HIGH, alice);
        var task2 = system.CreateTask("Write unit tests", "Cover auth module",
            new DateTime(2025, 8, 5), TaskPriority.MEDIUM, alice);
        var task3 = system.CreateTask("Fix navbar bug", "Hamburger menu broken",
            new DateTime(2025, 7, 28), TaskPriority.CRITICAL, bob);

        sprintList.AddTask(task1);
        sprintList.AddTask(task2);
        sprintList.AddTask(task3);

        // ── Assign + Tags ──
        Console.WriteLine("\n=== Assign + Tags ===\n");
        task1.Assign(alice);
        task2.Assign(bob);
        task3.Assign(bob);
        task1.AddTag(new Tag("frontend"));
        task1.AddTag(new Tag("auth"));
        task3.AddTag(new Tag("bug"));

        // ── Subtasks ──
        Console.WriteLine("\n=== Subtasks ===\n");
        var sub1 = system.CreateTask("Google OAuth", "Integrate Google SSO",
            new DateTime(2025, 7, 30), TaskPriority.HIGH, alice);
        var sub2 = system.CreateTask("GitHub OAuth", "Integrate GitHub SSO",
            new DateTime(2025, 7, 31), TaskPriority.MEDIUM, alice);
        task1.AddSubtask(sub1);
        task1.AddSubtask(sub2);

        // ── State transitions ──
        Console.WriteLine("\n=== State Transitions ===\n");
        task3.StartProgress();        // TODO → IN_PROGRESS
        task3.CompleteTask();          // IN_PROGRESS → DONE

        task1.StartProgress();         // TODO → IN_PROGRESS
        task1.CompleteTask();          // FAIL: subtasks not done

        sub1.StartProgress();
        sub1.CompleteTask();
        sub2.StartProgress();
        sub2.CompleteTask();
        task1.CompleteTask();          // NOW succeeds — all subtasks done

        // ── Invalid transition ──
        Console.WriteLine("\n=== Invalid Transitions ===\n");
        task2.CompleteTask();          // FAIL: TODO → DONE not allowed

        // ── Reopen ──
        Console.WriteLine("\n=== Reopen ===\n");
        task3.ReopenTask();            // DONE → TODO

        // ── Comments ──
        Console.WriteLine("\n=== Comments ===\n");
        task1.AddComment(new Comment(bob, "Looks good, merging!"));
        task2.AddComment(new Comment(alice, "Let's prioritize this"));

        // ── Priority update ──
        Console.WriteLine("\n=== Priority Update ===\n");
        task2.UpdatePriority(TaskPriority.HIGH);

        // ── Display task list ──
        Console.WriteLine("\n=== Sprint 1 ===\n");
        sprintList.Display();

        // ── Filter by status ──
        Console.WriteLine("\n=== Filter: DONE tasks ===\n");
        var doneTasks = system.ListTasksByStatus(TaskStatus.DONE);
        foreach (var t in doneTasks)
            Console.WriteLine($"    {t}");

        // ── Search with sort ──
        Console.WriteLine("\n=== Search 'OAuth' sorted by priority ===\n");
        var results = system.SearchTasks("OAuth", new SortByPriority());
        foreach (var t in results)
            Console.WriteLine($"    {t}");

        // ── Activity log ──
        Console.WriteLine("\n=== Activity Log: task1 ===\n");
        foreach (var log in task1.ActivityLogs)
            Console.WriteLine($"    {log}");
    }
}
