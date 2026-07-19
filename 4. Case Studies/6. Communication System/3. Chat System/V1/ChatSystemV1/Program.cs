using System.Collections.Concurrent;

// Chat System V1
//
// Problem Statement:
//   A chat application enables real-time communication between users through
//   text-based messages. Supports one-on-one and group messaging.
//
// Core Entities:
//   User
//      - Id, Name
//      - Inbox: receives messages in real-time via OnMessageReceived callback
//   Message (immutable)
//      - Id, SenderId, Content, Timestamp
//      - Once sent, cannot be edited or deleted
//   Chat (abstract)
//      - ChatId, list of participants
//      - MessageHistory: ordered list of all messages (append-only)
//      - SendMessage(sender, content): creates message, appends to history, delivers to participants
//   OneOnOneChat : Chat
//      - Exactly 2 participants
//      - Deterministic chatId from sorted user IDs (ensures one DM per pair)
//   GroupChat : Chat
//      - Multiple participants, supports add/remove members
//      - Has a GroupName for display
//   ChatService
//      - Central coordinator — manages users and chats
//      - CreateOneOnOneChat(user1, user2)
//      - CreateGroupChat(name, members)
//      - SendMessage(chatId, sender, content)
//      - GetHistory(chatId): returns ordered message list
//
// Design Decisions:
//   - Message is immutable (readonly properties, no setters) → thread-safe, no corruption
//   - History is append-only (List<Message>, only Add, no Remove/Edit) → preserves ordering
//   - Lock on Send to preserve ordering under concurrency → multiple senders won't interleave
//   - ConcurrentDictionary for thread-safe user/chat lookups → concurrent registration safe
//   - Abstract Chat allows extension (channels, broadcast, etc.) → Open/Closed principle
//   - User.OnMessageReceived is a callback for real-time delivery → push model
//
// Overall Flow:
//   chatService.SendMessage(chatId, senderId, content)
//     → Chat.SendMessage(sender, content)
//       → Create immutable Message
//       → Append to history (under lock)
//       → Deliver to all participants (notify via callback)

// ─────────────────────────────────────────────
// Message (immutable)
// ─────────────────────────────────────────────

// Message is the fundamental data unit of the chat system.
// It is immutable — once created, its state never changes.
// This guarantees thread-safety and satisfies the "no edit/delete" requirement.
public class Message
{
    public string Id { get; }          // Unique identifier for the message
    public string SenderId { get; }    // Who sent this message
    public string Content { get; }     // The text content (cannot be changed after creation)
    public DateTime Timestamp { get; } // When the message was created

    public Message(string senderId, string content)
    {
        Id = Guid.NewGuid().ToString("N")[..8]; // Short unique ID for readability
        SenderId = senderId;
        Content = content;
        Timestamp = DateTime.Now;
        // No setters exposed — once constructed, this object is frozen
    }

    public override string ToString() => $"[{Timestamp:HH:mm:ss}] {SenderId}: {Content}";
}

// ─────────────────────────────────────────────
// User
// ─────────────────────────────────────────────

// Represents a participant in the chat system.
// Each user has a unique Id and a display Name.
// The OnMessageReceived callback simulates real-time delivery —
// in a real system this would push to a WebSocket/SSE connection.
public class User
{
    public string Id { get; }
    public string Name { get; }

    public User(string id, string name)
    {
        Id = id;
        Name = name;
    }

    // Called by Chat.SendMessage() to notify this user of a new message.
    // Skips if the user is the sender (no need to echo your own message).
    // In production, this would push to the user's active connection.
    public void OnMessageReceived(string chatId, Message message)
    {
        if (message.SenderId != Id) // Don't notify sender of their own message
            Console.WriteLine($"    [{Name}] received in {chatId}: {message.Content} (from {message.SenderId})");
    }

    public override string ToString() => Name;
}

// ─────────────────────────────────────────────
// Chat (abstract)
// ─────────────────────────────────────────────

// Abstract base class for all chat types (DM, group, future: channels, broadcasts).
// Holds the participant list and the message history.
// 
// Key invariants:
//   - _history is append-only: messages are never removed or modified
//   - _lock ensures ordering: concurrent sends are serialized
//   - SendMessage validates the sender is a participant before proceeding
public abstract class Chat
{
    public string ChatId { get; }

    // Participant list — subclasses manage adding/removing
    protected readonly List<User> _participants = new();

    // Append-only message log. This is the source of truth for chat history.
    // Only Add() is called, never Remove() or []= assignment.
    private readonly List<Message> _history = new();

    // Lock ensures messages are appended in order even under concurrent sends.
    // Without this, two simultaneous SendMessage calls could interleave.
    private readonly object _lock = new();

    protected Chat(string chatId)
    {
        ChatId = chatId;
    }

    // Expose participants as read-only to prevent external modification
    public IReadOnlyList<User> Participants => _participants.AsReadOnly();

    // Returns a snapshot copy of the history.
    // Callers get a frozen view — subsequent messages won't appear in the returned list.
    // This is thread-safe: we copy under lock.
    public IReadOnlyList<Message> GetHistory()
    {
        lock (_lock)
        {
            return _history.ToList().AsReadOnly();
        }
    }

    // Core operation: create message → append to history → deliver to participants
    public void SendMessage(User sender, string content)
    {
        // 1. Validate: only participants can send messages in this chat
        if (!_participants.Contains(sender))
            throw new InvalidOperationException($"{sender.Name} is not a participant in chat {ChatId}");

        // 2. Create an immutable message (timestamp is set at creation time)
        var message = new Message(sender.Id, content);

        // 3. Append to history under lock — guarantees ordering
        //    If two threads call SendMessage simultaneously, one will wait.
        //    This ensures _history[i].Timestamp <= _history[i+1].Timestamp
        lock (_lock)
        {
            _history.Add(message);
        }

        // 4. Deliver to all participants (real-time notification)
        //    Each participant's OnMessageReceived is called synchronously.
        //    The sender's callback will skip (no self-echo).
        foreach (var participant in _participants)
        {
            participant.OnMessageReceived(ChatId, message);
        }
    }
}

// ─────────────────────────────────────────────
// OneOnOneChat
// ─────────────────────────────────────────────

// A direct message (DM) between exactly two users.
// Participants are fixed at creation — no add/remove.
// The chatId is deterministic: "dm:{sortedId1}:{sortedId2}"
// This ensures that Alice→Bob and Bob→Alice resolve to the same chat.
public class OneOnOneChat : Chat
{
    public OneOnOneChat(string chatId, User user1, User user2) : base(chatId)
    {
        _participants.Add(user1);
        _participants.Add(user2);
        // Exactly 2 participants, no more, no less
    }
}

// ─────────────────────────────────────────────
// GroupChat
// ─────────────────────────────────────────────

// A multi-user chat with a display name.
// Supports dynamic membership: users can join and leave.
// Messages from after a user joins are visible to them;
// messages from before are also visible (since history is shared).
public class GroupChat : Chat
{
    public string GroupName { get; }

    public GroupChat(string chatId, string groupName, IEnumerable<User> members) : base(chatId)
    {
        GroupName = groupName;
        _participants.AddRange(members); // Initial members added at creation
    }

    // Add a new member to the group.
    // Idempotent: if already a member, does nothing.
    public void AddMember(User user)
    {
        if (!_participants.Contains(user))
        {
            _participants.Add(user);
            Console.WriteLine($"    [{user.Name}] joined group '{GroupName}'");
        }
    }

    // Remove a member from the group.
    // After removal, they won't receive new messages.
    public void RemoveMember(User user)
    {
        _participants.Remove(user);
        Console.WriteLine($"    [{user.Name}] left group '{GroupName}'");
    }
}

// ─────────────────────────────────────────────
// ChatService — central coordinator
// ─────────────────────────────────────────────

// ChatService is the public API for the chat system.
// It manages user registration, chat creation, message sending, and history retrieval.
// Uses ConcurrentDictionary for thread-safe lookups (multiple threads can register/send concurrently).
public class ChatService
{
    // Thread-safe registry of all users by ID
    private readonly ConcurrentDictionary<string, User> _users = new();

    // Thread-safe registry of all chats by chatId
    private readonly ConcurrentDictionary<string, Chat> _chats = new();

    // Register a user so they can participate in chats
    public void RegisterUser(User user)
    {
        _users.TryAdd(user.Id, user);
    }

    // Look up a user by ID. Throws if not found.
    public User GetUser(string userId)
    {
        if (_users.TryGetValue(userId, out var user)) return user;
        throw new ArgumentException($"User '{userId}' not found");
    }

    // Create a one-on-one DM between two users.
    // Uses sorted IDs for a deterministic chatId — calling with (Alice, Bob) or (Bob, Alice)
    // returns the same chat. If the chat already exists, returns the existing one.
    public OneOnOneChat CreateOneOnOneChat(User user1, User user2)
    {
        // Sort IDs to create a canonical key: "dm:alice:bob" regardless of argument order
        var ids = new[] { user1.Id, user2.Id }.OrderBy(x => x).ToArray();
        string chatId = $"dm:{ids[0]}:{ids[1]}";

        // Return existing if already created (idempotent)
        if (_chats.TryGetValue(chatId, out var existing))
            return (OneOnOneChat)existing;

        var chat = new OneOnOneChat(chatId, user1, user2);
        _chats.TryAdd(chatId, chat);
        return chat;
    }

    // Create a new group chat with a display name and initial members.
    // Each group gets a unique random chatId (not deterministic like DMs).
    public GroupChat CreateGroupChat(string groupName, IEnumerable<User> members)
    {
        string chatId = $"group:{Guid.NewGuid().ToString("N")[..6]}";
        var chat = new GroupChat(chatId, groupName, members);
        _chats.TryAdd(chatId, chat);
        return chat;
    }

    // Look up a chat by ID. Throws if not found.
    public Chat GetChat(string chatId)
    {
        if (_chats.TryGetValue(chatId, out var chat)) return chat;
        throw new ArgumentException($"Chat '{chatId}' not found");
    }

    // Send a message to a chat. Resolves sender from userId, then delegates to Chat.SendMessage.
    // This is the main entry point for sending messages.
    public void SendMessage(string chatId, string senderId, string content)
    {
        var chat = GetChat(chatId);
        var sender = GetUser(senderId);
        chat.SendMessage(sender, content); // → create msg → append history → notify participants
    }

    // Retrieve the full message history for a chat (ordered by timestamp).
    // Returns a snapshot — new messages after this call won't appear in the result.
    public IReadOnlyList<Message> GetHistory(string chatId)
    {
        return GetChat(chatId).GetHistory();
    }
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var chatService = new ChatService();

        // Register users
        var alice = new User("alice", "Alice");
        var bob = new User("bob", "Bob");
        var charlie = new User("charlie", "Charlie");

        chatService.RegisterUser(alice);
        chatService.RegisterUser(bob);
        chatService.RegisterUser(charlie);

        // ── One-on-One Chat ──
        // Creates a DM with chatId "dm:alice:bob" (sorted)
        Console.WriteLine("=== One-on-One: Alice ↔ Bob ===\n");
        var dm = chatService.CreateOneOnOneChat(alice, bob);

        chatService.SendMessage(dm.ChatId, "alice", "Hey Bob, how's it going?");
        chatService.SendMessage(dm.ChatId, "bob", "Hey Alice! All good, you?");
        chatService.SendMessage(dm.ChatId, "alice", "Great! Want to join the project group?");

        // ── Group Chat ──
        // Creates a group with Alice and Bob as initial members
        Console.WriteLine("\n=== Group Chat: Project Team ===\n");
        var group = chatService.CreateGroupChat("Project Team", new[] { alice, bob });

        chatService.SendMessage(group.ChatId, "alice", "Welcome to the project team!");
        chatService.SendMessage(group.ChatId, "bob", "Thanks! Excited to start.");

        // Charlie joins the group dynamically
        Console.WriteLine();
        ((GroupChat)chatService.GetChat(group.ChatId)).AddMember(charlie);
        chatService.SendMessage(group.ChatId, "charlie", "Hi everyone! Just joined.");
        chatService.SendMessage(group.ChatId, "alice", "Welcome Charlie!");

        // ── View Chat History ──
        // GetHistory returns all messages in the order they were sent
        Console.WriteLine("\n=== Chat History: Project Team ===\n");
        var history = chatService.GetHistory(group.ChatId);
        foreach (var msg in history)
        {
            Console.WriteLine($"  {msg}");
        }

        // ── Demonstrate immutability: history preserves order ──
        Console.WriteLine($"\n  Total messages in group: {history.Count}");
        Console.WriteLine($"  First message: {history[0].Content}");
        Console.WriteLine($"  Last message: {history[^1].Content}");
    }
}
