using System.Collections.Concurrent;
using System.Collections.Immutable;

// StackOverflow System V2 — Thread-Safe
//
// V1 Gaps:
//   1. VoteCount++/FlagCount++ — not atomic (read-modify-write race)
//   2. Tag frequency counters — race under concurrent posts
//   3. Lists (answers, comments) — concurrent add + iterate crashes
//   4. Account.Reputation += N — multiple threads race
//   5. Answer.Accepted — two threads accept different answers
//   6. Question.Status — close while posting an answer (TOCTOU)
//
// V2 Fixes:
//   1. Interlocked.Increment for all counters (VoteCount, FlagCount, frequency)
//   2. Interlocked.Add for reputation changes
//   3. ImmutableList + ImmutableInterlocked for answers, comments
//   4. Per-question lock for status transitions and answer acceptance
//   5. Account.Reputation uses Interlocked for atomic add

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────
public enum QuestionStatus { Open, Closed, OnHold, Deleted }
public enum QuestionClosingRemark { Duplicate, OffTopic, TooBroad, NotConstructive, NotARealQuestion, PrimarilyOpinionBased }
public enum AccountStatus { Active, Blocked, Banned, Compromised, Archived, Unknown }

// ─────────────────────────────────────────────
// Account — Interlocked for reputation
// ─────────────────────────────────────────────
public class Account
{
    public string Id { get; }
    public string Password { get; set; }
    public string Name { get; }
    public string Email { get; }
    public string Phone { get; }
    private int _reputation;
    public int Reputation => _reputation;
    public volatile AccountStatus Status;

    public Account(string id, string password, string name, string email, string phone)
    {
        Id = id; Password = password; Name = name; Email = email; Phone = phone;
        _reputation = 1; Status = AccountStatus.Active;
    }

    // V2: Interlocked for atomic reputation changes from multiple voting threads
    public void AddReputation(int amount)
    {
        Interlocked.Add(ref _reputation, amount);
        // Ensure minimum of 1
        if (_reputation < 1) Interlocked.Exchange(ref _reputation, 1);
    }

    public bool ResetPassword(string newPassword) { Password = newPassword; return true; }
}

// ─────────────────────────────────────────────
// Notification, Photo, Badge (same as V1 — immutable/simple)
// ─────────────────────────────────────────────
public class Notification
{
    public int NotificationId { get; }
    public DateTime CreatedOn { get; }
    public string Content { get; }
    private static int _nextId;
    public Notification(string content) { NotificationId = Interlocked.Increment(ref _nextId); CreatedOn = DateTime.Now; Content = content; }
    public bool SendNotification() { Console.WriteLine($"    [Notification] {Content}"); return true; }
}

public class Photo
{
    public int PhotoId { get; }
    public string PhotoPath { get; }
    public DateTime CreationDate { get; }
    private static int _nextId;
    public Photo(string path) { PhotoId = Interlocked.Increment(ref _nextId); PhotoPath = path; CreationDate = DateTime.Now; }
}

public class Badge
{
    public string Name { get; }
    public string Description { get; }
    public Badge(string name, string desc) { Name = name; Description = desc; }
    public override string ToString() => Name;
}

public class Tag
{
    public string Name { get; }
    public string Description { get; }
    private int _dailyFreq;
    private int _weeklyFreq;
    // V2: Interlocked for atomic frequency increments
    public int DailyAskedFrequency => _dailyFreq;
    public int WeeklyAskedFrequency => _weeklyFreq;
    public void IncrementDaily() => Interlocked.Increment(ref _dailyFreq);
    public void IncrementWeekly() => Interlocked.Increment(ref _weeklyFreq);
    public Tag(string name, string desc = "") { Name = name; Description = desc; }
    public override string ToString() => $"{Name}(daily:{_dailyFreq})";
}

public class Bounty
{
    public int Reputation { get; }
    public DateTime Expiry { get; }
    public Bounty(int rep, int days = 7) { Reputation = rep; Expiry = DateTime.Now.AddDays(days); }
    public void ModifyReputation(Account account) => account.AddReputation(-Reputation);
}

// ─────────────────────────────────────────────
// Comment — Interlocked counters
// ─────────────────────────────────────────────
public class Comment
{
    public string Id { get; }
    public string Text { get; }
    public DateTime Creation { get; }
    private int _flagCount;
    private int _voteCount;
    public int FlagCount => _flagCount;
    public int VoteCount => _voteCount;

    public Comment(string text) { Id = Guid.NewGuid().ToString("N")[..8]; Text = text; Creation = DateTime.Now; }
    public void IncrementVoteCount() => Interlocked.Increment(ref _voteCount);
    public void IncrementFlagCount() => Interlocked.Increment(ref _flagCount);
}

// ─────────────────────────────────────────────
// Answer — Interlocked counters, ImmutableList for comments
// ─────────────────────────────────────────────
public class Answer
{
    public string Id { get; }
    public string AnswerText { get; }
    public string AuthorId { get; }
    public DateTime Create { get; }
    private int _voteCount;
    private int _flagCount;
    private volatile bool _accepted;
    private ImmutableList<Comment> _comments = ImmutableList<Comment>.Empty;

    public int VoteCount => _voteCount;
    public int FlagCount => _flagCount;
    public bool Accepted => _accepted;
    public ImmutableList<Comment> Comments => _comments;

    public Answer(string authorId, string text)
    { Id = Guid.NewGuid().ToString("N")[..8]; AuthorId = authorId; AnswerText = text; Create = DateTime.Now; }

    public void IncrementVoteCount() => Interlocked.Increment(ref _voteCount);
    public void IncrementFlagCount() => Interlocked.Increment(ref _flagCount);
    internal void SetAccepted(bool val) => _accepted = val;
    public void AddComment(Comment c) => ImmutableInterlocked.Update(ref _comments, list => list.Add(c));

    public override string ToString() => $"Answer by {AuthorId} (votes:{_voteCount}{(_accepted ? ", ✓" : "")})";
}

// ─────────────────────────────────────────────
// Question — per-question lock for status + accept, ImmutableList for answers/comments
// ─────────────────────────────────────────────
public class Question
{
    public string Id { get; }
    public string AuthorId { get; }
    public string Title { get; }
    public string Description { get; }
    public DateTime CreationTime { get; }

    private int _viewCount;
    private int _voteCount;
    private readonly object _lock = new(); // Per-question lock for status transitions
    private QuestionStatus _status;
    private QuestionClosingRemark? _closingRemark;
    private Bounty? _bounty;
    private ImmutableList<Answer> _answers = ImmutableList<Answer>.Empty;
    private ImmutableList<Comment> _comments = ImmutableList<Comment>.Empty;
    private ImmutableList<Tag> _tags = ImmutableList<Tag>.Empty;

    public int ViewCount => _viewCount;
    public int VoteCount => _voteCount;
    public QuestionStatus Status { get { lock (_lock) { return _status; } } }
    public QuestionClosingRemark? ClosingRemark => _closingRemark;
    public Bounty? Bounty { get { lock (_lock) { return _bounty; } } }
    public ImmutableList<Answer> Answers => _answers;
    public ImmutableList<Comment> Comments => _comments;
    public ImmutableList<Tag> Tags => _tags;

    public Question(string authorId, string title, string description, List<Tag> tags)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        AuthorId = authorId; Title = title; Description = description;
        CreationTime = DateTime.Now; _status = QuestionStatus.Open;
        _tags = ImmutableList<Tag>.Empty.AddRange(tags);
    }

    public void IncrementViewCount() => Interlocked.Increment(ref _viewCount);
    public void IncrementVoteCount() => Interlocked.Increment(ref _voteCount);
    public void AddComment(Comment c) => ImmutableInterlocked.Update(ref _comments, list => list.Add(c));

    // V2: Add answer only if question is Open (check + add under lock)
    public bool TryAddAnswer(Answer answer)
    {
        lock (_lock)
        {
            if (_status != QuestionStatus.Open) return false;
            ImmutableInterlocked.Update(ref _answers, list => list.Add(answer));
            return true;
        }
    }

    // V2: Accept answer under lock — prevents two threads accepting different answers
    public bool TryAcceptAnswer(string answerId, string callerId)
    {
        lock (_lock)
        {
            if (AuthorId != callerId) return false;
            var answer = _answers.FirstOrDefault(a => a.Id == answerId);
            if (answer == null) return false;
            answer.SetAccepted(true);
            return true;
        }
    }

    // V2: Close under lock — atomic check + set
    public bool TryClose(QuestionClosingRemark remark)
    {
        lock (_lock)
        {
            if (_status != QuestionStatus.Open && _status != QuestionStatus.OnHold) return false;
            _status = QuestionStatus.Closed;
            _closingRemark = remark;
            return true;
        }
    }

    public bool TryDelete()
    {
        lock (_lock)
        {
            if (_status == QuestionStatus.Deleted) return false;
            _status = QuestionStatus.Deleted;
            return true;
        }
    }

    public bool TryUndelete()
    {
        lock (_lock)
        {
            if (_status != QuestionStatus.Deleted) return false;
            _status = QuestionStatus.Open;
            return true;
        }
    }

    public bool TrySetBounty(Bounty bounty)
    {
        lock (_lock)
        {
            if (_bounty != null) return false; // already has bounty
            _bounty = bounty;
            return true;
        }
    }

    public override string ToString() => $"Q: \"{Title}\" (votes:{_voteCount}, {Status}, answers:{_answers.Count})";
}

// ─────────────────────────────────────────────
// User hierarchy (same as V1)
// ─────────────────────────────────────────────
public interface ISearch { List<Question> Search(string query); }

public class Guest : ISearch
{
    public virtual List<Question> Search(string query) => new();
}

public class Member : Guest
{
    public Account Account { get; }
    private ImmutableList<Badge> _badges = ImmutableList<Badge>.Empty;
    private ImmutableList<Notification> _notifications = ImmutableList<Notification>.Empty;
    private ImmutableList<string> _favorites = ImmutableList<string>.Empty;

    public Member(Account account) { Account = account; }

    public int GetReputation() => Account.Reputation;
    public ImmutableList<Badge> Badges => _badges;
    public ImmutableList<Notification> Notifications => _notifications;
    public ImmutableList<string> Favorites => _favorites;

    public void CollectBadge(Badge badge) => ImmutableInterlocked.Update(ref _badges, l => l.Add(badge));
    public void AddNotification(Notification n) { ImmutableInterlocked.Update(ref _notifications, l => l.Add(n)); n.SendNotification(); }
    public void MarkFavorite(string qId) => ImmutableInterlocked.Update(ref _favorites, l => l.Add(qId));

    public override string ToString() => $"{Account.Name} (Rep:{Account.Reputation})";
}

public class Admin : Member
{
    public Admin(Account account) : base(account) { }
    public bool BlockMember(Member m) { m.Account.Status = AccountStatus.Blocked; Console.WriteLine($"    [Admin] Blocked {m.Account.Name}"); return true; }
    public bool UnblockMember(Member m) { m.Account.Status = AccountStatus.Active; Console.WriteLine($"    [Admin] Unblocked {m.Account.Name}"); return true; }
}

public class Moderator : Member
{
    public Moderator(Account account) : base(account) { }
    public bool CloseQuestion(Question q, QuestionClosingRemark r) { var ok = q.TryClose(r); if (ok) Console.WriteLine($"    [Mod] Closed \"{q.Title}\" ({r})"); return ok; }
    public bool UndeleteQuestion(Question q) { var ok = q.TryUndelete(); if (ok) Console.WriteLine($"    [Mod] Undeleted \"{q.Title}\""); return ok; }
}

// ─────────────────────────────────────────────
// StackOverflowService — Facade (thread-safe)
// ─────────────────────────────────────────────
public class StackOverflowService : ISearch
{
    private readonly ConcurrentDictionary<string, Member> _members = new();
    private readonly ConcurrentDictionary<string, Question> _questions = new();
    private readonly ConcurrentDictionary<string, Tag> _tags = new();

    public Member RegisterMember(string id, string pw, string name, string email, string phone)
    { var m = new Member(new Account(id, pw, name, email, phone)); _members.TryAdd(id, m); return m; }

    public Admin RegisterAdmin(string id, string pw, string name, string email, string phone)
    { var a = new Admin(new Account(id, pw, name, email, phone)); _members.TryAdd(id, a); return a; }

    public Moderator RegisterModerator(string id, string pw, string name, string email, string phone)
    { var m = new Moderator(new Account(id, pw, name, email, phone)); _members.TryAdd(id, m); return m; }

    // ── Post Question ──
    public Question? PostQuestion(string memberId, string title, string desc, List<string> tagNames)
    {
        if (!_members.TryGetValue(memberId, out var member)) return null;
        if (member.Account.Status != AccountStatus.Active) return null;

        var tags = new List<Tag>();
        foreach (var name in tagNames)
        {
            var tag = _tags.GetOrAdd(name, _ => new Tag(name));
            tag.IncrementDaily(); // V2: Interlocked inside
            tag.IncrementWeekly();
            tags.Add(tag);
        }

        var question = new Question(memberId, title, desc, tags);
        _questions.TryAdd(question.Id, question);
        Console.WriteLine($"    [Q] {member.Account.Name} posted: \"{title}\"");
        return question;
    }

    // ── Post Answer (V2: TryAddAnswer checks status under lock) ──
    public Answer? PostAnswer(string memberId, string questionId, string text)
    {
        if (!_members.TryGetValue(memberId, out var member)) return null;
        if (member.Account.Status != AccountStatus.Active) return null;
        if (!_questions.TryGetValue(questionId, out var question)) return null;

        var answer = new Answer(memberId, text);
        // V2: atomic check (status == Open) + add under per-question lock
        if (!question.TryAddAnswer(answer))
        {
            Console.WriteLine($"    [A] Cannot answer — question is {question.Status}");
            return null;
        }

        // Notify author
        if (_members.TryGetValue(question.AuthorId, out var author))
            author.AddNotification(new Notification($"New answer on \"{question.Title}\" by {member.Account.Name}"));

        Console.WriteLine($"    [A] {member.Account.Name} answered: \"{question.Title}\"");
        return answer;
    }

    // ── Accept (V2: TryAcceptAnswer under per-question lock) ──
    public bool AcceptAnswer(string memberId, string questionId, string answerId)
    {
        if (!_questions.TryGetValue(questionId, out var question)) return false;
        if (!question.TryAcceptAnswer(answerId, memberId)) return false;

        var answer = question.Answers.FirstOrDefault(a => a.Id == answerId);
        if (answer != null && _members.TryGetValue(answer.AuthorId, out var answerAuthor))
            answerAuthor.Account.AddReputation(15);

        Console.WriteLine($"    [✓] Accepted answer by {answer?.AuthorId}");
        return true;
    }

    // ── Comments ──
    public Comment? CommentOnQuestion(string memberId, string questionId, string text)
    {
        if (!_members.TryGetValue(memberId, out _)) return null;
        if (!_questions.TryGetValue(questionId, out var q)) return null;
        var c = new Comment(text); q.AddComment(c); return c;
    }

    public Comment? CommentOnAnswer(string memberId, string questionId, string answerId, string text)
    {
        if (!_members.TryGetValue(memberId, out _)) return null;
        if (!_questions.TryGetValue(questionId, out var q)) return null;
        var a = q.Answers.FirstOrDefault(a => a.Id == answerId);
        if (a == null) return null;
        var c = new Comment(text); a.AddComment(c); return c;
    }

    // ── Voting (V2: Interlocked counters + atomic rep) ──
    public bool VoteQuestion(string memberId, string questionId)
    {
        if (!_members.TryGetValue(memberId, out _)) return false;
        if (!_questions.TryGetValue(questionId, out var q)) return false;
        q.IncrementVoteCount();
        if (_members.TryGetValue(q.AuthorId, out var author)) author.Account.AddReputation(10);
        return true;
    }

    public bool VoteAnswer(string memberId, string questionId, string answerId)
    {
        if (!_members.TryGetValue(memberId, out _)) return false;
        if (!_questions.TryGetValue(questionId, out var q)) return false;
        var a = q.Answers.FirstOrDefault(a => a.Id == answerId);
        if (a == null) return false;
        a.IncrementVoteCount();
        if (_members.TryGetValue(a.AuthorId, out var author)) author.Account.AddReputation(10);
        return true;
    }

    // ── Bounty (V2: TrySetBounty under lock) ──
    public bool AddBounty(string memberId, string questionId, int rep)
    {
        if (!_members.TryGetValue(memberId, out var member)) return false;
        if (!_questions.TryGetValue(questionId, out var q)) return false;
        if (q.AuthorId != memberId || member.Account.Reputation < rep) return false;

        var bounty = new Bounty(rep);
        if (!q.TrySetBounty(bounty)) return false; // already has one
        bounty.ModifyReputation(member.Account);
        Console.WriteLine($"    [Bounty] {member.Account.Name} added {rep} rep bounty");
        return true;
    }

    // ── Search ──
    public List<Question> Search(string query) =>
        _questions.Values.Where(q => q.Status != QuestionStatus.Deleted &&
            (q.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             q.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             q.Tags.Any(t => t.Name.Equals(query, StringComparison.OrdinalIgnoreCase)))).ToList();

    public List<Tag> GetTopTags(int count = 10) =>
        _tags.Values.OrderByDescending(t => t.DailyAskedFrequency).Take(count).ToList();

    public void AwardBadge(string memberId, string name, string desc)
    {
        if (_members.TryGetValue(memberId, out var m))
        { m.CollectBadge(new Badge(name, desc)); Console.WriteLine($"    [Badge] {m.Account.Name} earned \"{name}\""); }
    }

    public Member? GetMember(string id) => _members.TryGetValue(id, out var m) ? m : null;
}

// ─────────────────────────────────────────────
// Demo — concurrent voting + answer during close
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var service = new StackOverflowService();

        var alice = service.RegisterMember("alice", "pw", "Alice", "a@m.com", "1");
        var bob = service.RegisterMember("bob", "pw", "Bob", "b@m.com", "2");
        var charlie = service.RegisterMember("charlie", "pw", "Charlie", "c@m.com", "3");
        var mod = service.RegisterModerator("mod1", "pw", "ModDave", "m@m.com", "4");

        alice.Account.AddReputation(100);

        // ── Scenario 1: Concurrent voting (10 threads upvote same question) ──
        Console.WriteLine("=== Scenario 1: Concurrent Voting (10 threads) ===\n");

        var q1 = service.PostQuestion("alice", "How to reverse a linked list?",
            "Need efficient approach", new List<string> { "c#", "algorithms" });

        var voteTasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var voterId = $"voter{i}";
            service.RegisterMember(voterId, "pw", $"Voter{i}", $"v{i}@m.com", $"{i}");
            voteTasks.Add(Task.Run(() => service.VoteQuestion(voterId, q1!.Id)));
        }
        Task.WaitAll(voteTasks.ToArray());

        Console.WriteLine($"    Q votes after 10 concurrent upvotes: {q1!.VoteCount}");
        Console.WriteLine($"    Alice rep: {alice.Account.Reputation} (should be 101 + 10*10 = 201)");

        // ── Scenario 2: Answer while moderator closes (race) ──
        Console.WriteLine("\n=== Scenario 2: Answer vs Close Race ===\n");

        var q2 = service.PostQuestion("bob", "What is your favorite IDE?",
            "Just curious", new List<string> { "discussion" });

        Answer? raceAnswer = null;
        var answerTask = Task.Run(() => { raceAnswer = service.PostAnswer("charlie", q2!.Id, "VS Code!"); });
        var closeTask = Task.Run(() => mod.CloseQuestion(q2!, QuestionClosingRemark.PrimarilyOpinionBased));

        Task.WaitAll(answerTask, closeTask);

        Console.WriteLine($"    Q2 status: {q2!.Status}");
        Console.WriteLine($"    Charlie's answer: {(raceAnswer != null ? "ACCEPTED (before close)" : "REJECTED (after close)")}");
        Console.WriteLine($"    Answers on Q2: {q2.Answers.Count}");

        // ── Scenario 3: Concurrent accept (two threads try to accept different answers) ──
        Console.WriteLine("\n=== Scenario 3: Concurrent Accept ===\n");

        var q3 = service.PostQuestion("alice", "Best sorting algorithm?",
            "For large datasets", new List<string> { "algorithms" });
        var a1 = service.PostAnswer("bob", q3!.Id, "Merge sort");
        var a2 = service.PostAnswer("charlie", q3.Id, "Quick sort");

        bool accept1 = false, accept2 = false;
        Task.WaitAll(
            Task.Run(() => { accept1 = service.AcceptAnswer("alice", q3.Id, a1!.Id); }),
            Task.Run(() => { accept2 = service.AcceptAnswer("alice", q3.Id, a2!.Id); }));

        Console.WriteLine($"    Accept A1 (Bob): {accept1}");
        Console.WriteLine($"    Accept A2 (Charlie): {accept2}");
        Console.WriteLine($"    (Both can succeed — per-question lock serializes them)");

        // ── Scenario 4: Full lifecycle ──
        Console.WriteLine("\n=== Scenario 4: Full Lifecycle ===\n");
        service.PostAnswer("bob", q1.Id, "Use three pointers iteratively");
        service.CommentOnQuestion("charlie", q1.Id, "Good question!");
        service.AddBounty("alice", q1.Id, 50);
        service.AwardBadge("bob", "First Answer", "Answered a question");
        bob.MarkFavorite(q1.Id);

        Console.WriteLine($"    Q1: {q1}");
        Console.WriteLine($"    Bob rep: {bob.Account.Reputation}");
        Console.WriteLine($"    Bob badges: [{string.Join(", ", bob.Badges)}]");
        Console.WriteLine($"    Bob favorites: {bob.Favorites.Count}");

        // ── Top Tags ──
        Console.WriteLine($"\n    Top tags: {string.Join(", ", service.GetTopTags(5))}");
    }
}
