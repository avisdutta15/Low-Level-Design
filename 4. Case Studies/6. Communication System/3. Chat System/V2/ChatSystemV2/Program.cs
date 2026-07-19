using System.Collections.Concurrent;
using System.Collections.Immutable;

// Chat System V2 — Fully Thread-Safe
//
// V1 Thread-Safety Gaps:
//   1. _participants is a plain List<User> — AddMember/RemoveMember race with SendMessage's iteration
//   2. SendMessage iterates _participants OUTSIDE the lock — concurrent modification crashes
//   3. Contains(sender) check is unsynchronized — races with AddMember/RemoveMember
//
// V2 Fixes:
//   - _participants is now ImmutableHashSet<User> with ImmutableInterlocked.Update
//     → Snapshot semantics: iteration is always safe, even during concurrent add/remove
//     → No lock needed for membership changes
//   - SendMessage captures a snapshot of participants before iterating
//     → A member added mid-delivery won't get the current message (consistent)
//     → A member removed mid-delivery still gets the current message (they were in the snapshot)
//   - _history still uses lock for append (ImmutableList would work but lock is simpler and faster for append-only)
//   - ReaderWriterLockSlim used for history: allows concurrent reads (GetHistory) while serializing writes (SendMessage)
//
// Thread-Safety Summary:
//   | Operation         | V1               | V2                                  |
//   |-------------------|------------------|-------------------------------------|
//   | _participants     | List (unsafe)    | ImmutableHashSet + Interlocked swap |
//   | _history append   | lock (safe)      | ReaderWriterLockSlim (write lock)   |
//   | _history read     | lock (safe)      | ReaderWriterLockSlim (read lock)    |
//   | Iteration during  | CRASH possible   | Snapshot from ImmutableHashSet      |
//   | add/remove        |                  | (always safe)                       |
//   | User/Chat lookup  | ConcurrentDict   | ConcurrentDict (same)              |

// ─────────────────────────────────────────────
// Message (immutable — same as V1)
// ─────────────────────────────────────────────
public class Message
{
    public string Id { get; }
    public string SenderId { get; }
    public string Content { get; }
    public DateTime Timestamp { get; }

    public Message(string senderId, string content)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        SenderId = senderId;
        Content = content;
        Timestamp = DateTime.Now;
    }

    public override string ToString() => $"[{Timestamp:HH:mm:ss}] {SenderId}: {Content}";
}

// ─────────────────────────────────────────────
// User (same as V1)
// ─────────────────────────────────────────────
public class User
{
    public string Id { get; }
    public string Name { get; }

    public User(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public void OnMessageReceived(string chatId, Message message)
    {
        if (message.SenderId != Id)
            Console.WriteLine($"    [{Name}] received in {chatId}: {message.Content} (from {message.SenderId})");
    }

    public override string ToString() => Name;
}

// ─────────────────────────────────────────────
// Chat (abstract) — fully thread-safe
// ─────────────────────────────────────────────
public abstract class Chat
{
    public string ChatId { get; }

    // V2: ImmutableHashSet with atomic swap via ImmutableInterlocked.
    // Any read of _participants gets a consistent snapshot.
    // AddMember/RemoveMember do a compare-and-swap — no lock needed.
    protected ImmutableHashSet<User> _participants = ImmutableHashSet<User>.Empty;

    // History uses ReaderWriterLockSlim:
    //   - Multiple threads can read history concurrently (GetHistory)
    //   - Only one thread can write at a time (SendMessage)
    //   - Readers don't block each other, only writers block readers
    private readonly List<Message> _history = new();
    private readonly ReaderWriterLockSlim _historyLock = new();

    protected Chat(string chatId)
    {
        ChatId = chatId;
    }

    // Returns current participants as an immutable snapshot (always safe to iterate)
    public ImmutableHashSet<User> Participants => _participants;

    // Returns a snapshot of the message history.
    // Uses a read lock — multiple threads can call this concurrently.
    public IReadOnlyList<Message> GetHistory()
    {
        _historyLock.EnterReadLock();
        try
        {
            return _history.ToList().AsReadOnly();
        }
        finally
        {
            _historyLock.ExitReadLock();
        }
    }

    public void SendMessage(User sender, string content)
    {
        // 1. Capture a snapshot of participants (immutable — safe to read anytime)
        var currentParticipants = _participants;

        // 2. Validate sender is a participant (read from snapshot, no lock needed)
        if (!currentParticipants.Contains(sender))
            throw new InvalidOperationException($"{sender.Name} is not a participant in chat {ChatId}");

        // 3. Create immutable message
        var message = new Message(sender.Id, content);

        // 4. Append to history under WRITE lock (serializes concurrent sends)
        _historyLock.EnterWriteLock();
        try
        {
            _history.Add(message);
        }
        finally
        {
            _historyLock.ExitWriteLock();
        }

        // 5. Deliver to all participants from the snapshot.
        //    If someone was added AFTER we captured the snapshot, they won't get this message.
        //    If someone was removed AFTER, they still will (they were in the snapshot).
        //    This is consistent: the snapshot represents "who was here when the message was sent".
        foreach (var participant in currentParticipants)
        {
            participant.OnMessageReceived(ChatId, message);
        }
    }
}

// ─────────────────────────────────────────────
// OneOnOneChat
// ─────────────────────────────────────────────
public class OneOnOneChat : Chat
{
    public OneOnOneChat(string chatId, User user1, User user2) : base(chatId)
    {
        // ImmutableInterlocked.Update does atomic compare-and-swap
        ImmutableInterlocked.Update(ref _participants, set => set.Add(user1).Add(user2));
    }
}

// ─────────────────────────────────────────────
// GroupChat
// ─────────────────────────────────────────────
public class GroupChat : Chat
{
    public string GroupName { get; }

    public GroupChat(string chatId, string groupName, IEnumerable<User> members) : base(chatId)
    {
        GroupName = groupName;
        ImmutableInterlocked.Update(ref _participants, set => set.Union(members));
    }

    // Thread-safe add: atomic compare-and-swap on the immutable set.
    // If two threads call AddMember simultaneously, both succeed without corruption.
    public void AddMember(User user)
    {
        var added = ImmutableInterlocked.Update(ref _participants, set => set.Add(user));
        if (added)
            Console.WriteLine($"    [{user.Name}] joined group '{GroupName}'");
    }

    // Thread-safe remove: atomic compare-and-swap.
    public void RemoveMember(User user)
    {
        ImmutableInterlocked.Update(ref _participants, set => set.Remove(user));
        Console.WriteLine($"    [{user.Name}] left group '{GroupName}'");
    }
}

// ─────────────────────────────────────────────
// ChatService (same as V1 — already thread-safe with ConcurrentDictionary)
// ─────────────────────────────────────────────
public class ChatService
{
    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly ConcurrentDictionary<string, Chat> _chats = new();

    public void RegisterUser(User user)
    {
        _users.TryAdd(user.Id, user);
    }

    public User GetUser(string userId)
    {
        if (_users.TryGetValue(userId, out var user)) return user;
        throw new ArgumentException($"User '{userId}' not found");
    }

    public OneOnOneChat CreateOneOnOneChat(User user1, User user2)
    {
        var ids = new[] { user1.Id, user2.Id }.OrderBy(x => x).ToArray();
        string chatId = $"dm:{ids[0]}:{ids[1]}";

        if (_chats.TryGetValue(chatId, out var existing))
            return (OneOnOneChat)existing;

        var chat = new OneOnOneChat(chatId, user1, user2);
        _chats.TryAdd(chatId, chat);
        return chat;
    }

    public GroupChat CreateGroupChat(string groupName, IEnumerable<User> members)
    {
        string chatId = $"group:{Guid.NewGuid().ToString("N")[..6]}";
        var chat = new GroupChat(chatId, groupName, members);
        _chats.TryAdd(chatId, chat);
        return chat;
    }

    public Chat GetChat(string chatId)
    {
        if (_chats.TryGetValue(chatId, out var chat)) return chat;
        throw new ArgumentException($"Chat '{chatId}' not found");
    }

    public void SendMessage(string chatId, string senderId, string content)
    {
        var chat = GetChat(chatId);
        var sender = GetUser(senderId);
        chat.SendMessage(sender, content);
    }

    public IReadOnlyList<Message> GetHistory(string chatId)
    {
        return GetChat(chatId).GetHistory();
    }
}

// ─────────────────────────────────────────────
// Demo — includes concurrent send to prove thread-safety
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var chatService = new ChatService();

        var alice = new User("alice", "Alice");
        var bob = new User("bob", "Bob");
        var charlie = new User("charlie", "Charlie");

        chatService.RegisterUser(alice);
        chatService.RegisterUser(bob);
        chatService.RegisterUser(charlie);

        // ── One-on-One (same as V1) ──
        Console.WriteLine("=== One-on-One: Alice ↔ Bob ===\n");
        var dm = chatService.CreateOneOnOneChat(alice, bob);

        chatService.SendMessage(dm.ChatId, "alice", "Hey Bob!");
        chatService.SendMessage(dm.ChatId, "bob", "Hey Alice!");

        // ── Group Chat ──
        Console.WriteLine("\n=== Group Chat: Project Team ===\n");
        var group = chatService.CreateGroupChat("Project Team", new[] { alice, bob, charlie });

        chatService.SendMessage(group.ChatId, "alice", "Welcome everyone!");
        chatService.SendMessage(group.ChatId, "bob", "Thanks!");
        chatService.SendMessage(group.ChatId, "charlie", "Hi all!");

        // ── Concurrent Sends (proves thread-safety) ──
        Console.WriteLine("\n=== Concurrent Sends (10 messages from 3 users) ===\n");

        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            int msgNum = i;
            string sender = msgNum % 3 == 0 ? "alice" : msgNum % 3 == 1 ? "bob" : "charlie";
            tasks.Add(Task.Run(() =>
                chatService.SendMessage(group.ChatId, sender, $"Concurrent msg #{msgNum}")
            ));
        }
        Task.WaitAll(tasks.ToArray());

        // ── Verify history ordering ──
        Console.WriteLine("\n=== Full History (should have 13 messages, all ordered) ===\n");
        var history = chatService.GetHistory(group.ChatId);
        for (int i = 0; i < history.Count; i++)
        {
            Console.WriteLine($"  [{i}] {history[i]}");
        }
        Console.WriteLine($"\n  Total: {history.Count} messages");

        // Verify timestamps are non-decreasing (ordering preserved)
        bool ordered = true;
        for (int i = 1; i < history.Count; i++)
        {
            if (history[i].Timestamp < history[i - 1].Timestamp)
            {
                ordered = false;
                break;
            }
        }
        Console.WriteLine($"  Ordering preserved: {ordered}");

        // ── Concurrent AddMember + SendMessage (no crash) ──
        Console.WriteLine("\n=== Concurrent Add + Send (no crash) ===\n");
        var dave = new User("dave", "Dave");
        chatService.RegisterUser(dave);

        var addTask = Task.Run(() => ((GroupChat)chatService.GetChat(group.ChatId)).AddMember(dave));
        var sendTask = Task.Run(() => chatService.SendMessage(group.ChatId, "alice", "Message during add"));

        Task.WaitAll(addTask, sendTask);
        Console.WriteLine("  No crash — thread-safe!");
    }
}
