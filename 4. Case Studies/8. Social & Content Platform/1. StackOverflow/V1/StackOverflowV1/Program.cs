using System.Collections.Concurrent;

// StackOverflow System V1
//
// Based on the class diagram:
//   Account          - id, password, name, email, phone, reputation, status
//   Guest            - registerAccount()
//   Member (extends Guest) - getReputation(), getEmail(), createQuestion()
//   Admin (extends Member) - blockMember(), unblockMember()
//   Moderator (extends Member) - closeQuestion(), undeleteQuestion()
//   Question         - title, description, viewCount, voteCount, status, closingRemark, bounty
//   Answer           - answerText, accepted, voteCount, flagCount, create datetime
//   Comment          - text, creation datetime, flagCount, voteCount
//   Tag              - name, description, dailyAskedFrequency, weeklyAskedFrequency
//   Bounty           - reputation, expiry, modifyReputation()
//   Badge            - name, description
//   Notification     - notificationId, createdOn, content, sendNotification()
//   Photo            - photoId, photoPath, creationDate, delete()
//   ISearch          - interface for search functionality
//
// Enums:
//   QuestionStatus       - Open, Closed, OnHold, Deleted
//   QuestionClosingRemark - Duplicate, OffTopic, TooBroad, NotConstructive, NotARealQuestion, PrimarilyOpinionBased
//   AccountStatus        - Active, Blocked, Banned, Compromised, Archived, Unknown

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────
public enum QuestionStatus { Open, Closed, OnHold, Deleted }

public enum QuestionClosingRemark
{
    Duplicate,
    OffTopic,
    TooBroad,
    NotConstructive,
    NotARealQuestion,
    PrimarilyOpinionBased
}

public enum AccountStatus { Active, Blocked, Banned, Compromised, Archived, Unknown }

// ─────────────────────────────────────────────
// Account
// ─────────────────────────────────────────────
public class Account
{
    public string Id { get; }
    public string Password { get; set; }
    public string Name { get; }
    public string Email { get; }
    public string Phone { get; }
    public int Reputation { get; set; }
    public AccountStatus Status { get; set; }

    public Account(string id, string password, string name, string email, string phone)
    {
        Id = id; Password = password; Name = name; Email = email; Phone = phone;
        Reputation = 1; Status = AccountStatus.Active;
    }

    public bool ResetPassword(string newPassword) { Password = newPassword; return true; }
}

// ─────────────────────────────────────────────
// Notification
// ─────────────────────────────────────────────
public class Notification
{
    public int NotificationId { get; }
    public DateTime CreatedOn { get; }
    public string Content { get; }

    private static int _nextId = 1;

    public Notification(string content)
    {
        NotificationId = _nextId++;
        CreatedOn = DateTime.Now;
        Content = content;
    }

    public bool SendNotification()
    {
        Console.WriteLine($"    [Notification] {Content}");
        return true;
    }
}

// ─────────────────────────────────────────────
// Photo
// ─────────────────────────────────────────────
public class Photo
{
    public int PhotoId { get; }
    public string PhotoPath { get; }
    public DateTime CreationDate { get; }

    private static int _nextId = 1;

    public Photo(string photoPath)
    {
        PhotoId = _nextId++;
        PhotoPath = photoPath;
        CreationDate = DateTime.Now;
    }

    public bool Delete() { Console.WriteLine($"    [Photo] Deleted: {PhotoPath}"); return true; }
}

// ─────────────────────────────────────────────
// Badge
// ─────────────────────────────────────────────
public class Badge
{
    public string Name { get; }
    public string Description { get; }

    public Badge(string name, string description) { Name = name; Description = description; }
    public override string ToString() => Name;
}

// ─────────────────────────────────────────────
// Tag
// ─────────────────────────────────────────────
public class Tag
{
    public string Name { get; }
    public string Description { get; }
    public int DailyAskedFrequency { get; set; }
    public int WeeklyAskedFrequency { get; set; }

    public Tag(string name, string description = "")
    {
        Name = name; Description = description;
    }

    public override string ToString() => $"{Name}(daily:{DailyAskedFrequency}, weekly:{WeeklyAskedFrequency})";
}

// ─────────────────────────────────────────────
// Bounty
// ─────────────────────────────────────────────
public class Bounty
{
    public int Reputation { get; }
    public DateTime Expiry { get; }

    public Bounty(int reputation, int daysToExpire = 7)
    {
        Reputation = reputation;
        Expiry = DateTime.Now.AddDays(daysToExpire);
    }

    public bool ModifyReputation(Account account)
    {
        account.Reputation -= Reputation;
        return true;
    }
}

// ─────────────────────────────────────────────
// Comment
// ─────────────────────────────────────────────
public class Comment
{
    public string Id { get; }
    public string Text { get; }
    public DateTime Creation { get; }
    public int FlagCount { get; private set; }
    public int VoteCount { get; private set; }

    public Comment(string text)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Text = text;
        Creation = DateTime.Now;
    }

    public void IncrementVoteCount() => VoteCount++;
    public void IncrementFlagCount() => FlagCount++;

    public override string ToString() => $"Comment(\"{Text}\", votes:{VoteCount})";
}

// ─────────────────────────────────────────────
// Answer
// ─────────────────────────────────────────────
public class Answer
{
    public string Id { get; }
    public string AnswerText { get; }
    public bool Accepted { get; set; }
    public int VoteCount { get; private set; }
    public int FlagCount { get; private set; }
    public DateTime Create { get; }
    public string AuthorId { get; }

    private readonly List<Comment> _comments = new();
    private readonly List<Photo> _photos = new();

    public Answer(string authorId, string answerText)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        AuthorId = authorId;
        AnswerText = answerText;
        Create = DateTime.Now;
    }

    public IReadOnlyList<Comment> Comments => _comments.AsReadOnly();
    public void AddComment(Comment comment) => _comments.Add(comment);
    public void AddPhoto(Photo photo) => _photos.Add(photo);
    public void IncrementFlagCount() => FlagCount++;
    public void IncrementVoteCount() => VoteCount++;

    public override string ToString() => $"Answer by {AuthorId} (votes:{VoteCount}{(Accepted ? ", ✓" : "")})";
}

// ─────────────────────────────────────────────
// Question
// ─────────────────────────────────────────────
public class Question
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public int ViewCount { get; set; }
    public int VoteCount { get; private set; }
    public DateTime CreationTime { get; }
    public DateTime UpdateTime { get; set; }
    public QuestionStatus Status { get; set; }
    public QuestionClosingRemark? ClosingRemark { get; set; }
    public Bounty? Bounty { get; set; }
    public string AuthorId { get; }

    private readonly List<Tag> _tags = new();
    private readonly List<Answer> _answers = new();
    private readonly List<Comment> _comments = new();
    private readonly List<Photo> _photos = new();

    public Question(string authorId, string title, string description, List<Tag> tags)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        AuthorId = authorId;
        Title = title;
        Description = description;
        CreationTime = DateTime.Now;
        UpdateTime = DateTime.Now;
        Status = QuestionStatus.Open;
        _tags.AddRange(tags);
    }

    public IReadOnlyList<Tag> Tags => _tags.AsReadOnly();
    public IReadOnlyList<Answer> Answers => _answers.AsReadOnly();
    public IReadOnlyList<Comment> Comments => _comments.AsReadOnly();

    public void AddAnswer(Answer answer) { _answers.Add(answer); UpdateTime = DateTime.Now; }
    public void AddComment(Comment comment) { _comments.Add(comment); UpdateTime = DateTime.Now; }
    public void AddPhoto(Photo photo) => _photos.Add(photo);
    public void IncrementVoteCount() => VoteCount++;

    // Close the question with a remark
    public bool Close(QuestionClosingRemark remark)
    {
        if (Status != QuestionStatus.Open && Status != QuestionStatus.OnHold) return false;
        Status = QuestionStatus.Closed;
        ClosingRemark = remark;
        return true;
    }

    // Undelete (moderator action)
    public bool Undelete()
    {
        if (Status != QuestionStatus.Deleted) return false;
        Status = QuestionStatus.Open;
        return true;
    }

    public override string ToString() =>
        $"Q: \"{Title}\" (views:{ViewCount}, votes:{VoteCount}, {Status}, answers:{_answers.Count})";
}

// ─────────────────────────────────────────────
// ISearch interface
// ─────────────────────────────────────────────
public interface ISearch
{
    List<Question> Search(string query);
}

// ─────────────────────────────────────────────
// Guest → Member → Admin / Moderator hierarchy
// ─────────────────────────────────────────────

// Guest can only register and search
public class Guest : ISearch
{
    public Account? RegisterAccount(string id, string password, string name, string email, string phone)
    {
        return new Account(id, password, name, email, phone);
    }

    public virtual List<Question> Search(string query) => new(); // overridden with service
}

// Member extends Guest: can post questions, answers, comments, vote, flag, add bounty, collect badges
public class Member : Guest
{
    public Account Account { get; }
    private readonly List<Badge> _badges = new();
    private readonly List<Notification> _notifications = new();

    public Member(Account account) { Account = account; }

    public int GetReputation() => Account.Reputation;
    public string GetEmail() => Account.Email;

    // Creates a question (delegates to service in practice, but this satisfies the class diagram contract)
    public bool CreateQuestion() => Account.Status == AccountStatus.Active;

    public void CollectBadge(Badge badge) { _badges.Add(badge); }
    public IReadOnlyList<Badge> Badges => _badges.AsReadOnly();

    public void AddNotification(Notification notification)
    {
        _notifications.Add(notification);
        notification.SendNotification();
    }

    public IReadOnlyList<Notification> Notifications => _notifications.AsReadOnly();

    // Mark a question as favorite (simplified — just tracks it)
    private readonly List<string> _favorites = new();
    public void MarkFavorite(string questionId) => _favorites.Add(questionId);
    public IReadOnlyList<string> Favorites => _favorites.AsReadOnly();

    public override string ToString() => $"{Account.Name} (Rep:{Account.Reputation})";
}

// Admin extends Member: can block/unblock members
public class Admin : Member
{
    public Admin(Account account) : base(account) { }

    public bool BlockMember(Member member)
    {
        member.Account.Status = AccountStatus.Blocked;
        Console.WriteLine($"    [Admin] {Account.Name} blocked {member.Account.Name}");
        return true;
    }

    public bool UnblockMember(Member member)
    {
        member.Account.Status = AccountStatus.Active;
        Console.WriteLine($"    [Admin] {Account.Name} unblocked {member.Account.Name}");
        return true;
    }
}

// Moderator extends Member: can close/undelete questions
public class Moderator : Member
{
    public Moderator(Account account) : base(account) { }

    public bool CloseQuestion(Question question, QuestionClosingRemark remark)
    {
        bool result = question.Close(remark);
        if (result) Console.WriteLine($"    [Mod] {Account.Name} closed \"{question.Title}\" ({remark})");
        return result;
    }

    public bool UndeleteQuestion(Question question)
    {
        bool result = question.Undelete();
        if (result) Console.WriteLine($"    [Mod] {Account.Name} undeleted \"{question.Title}\"");
        return result;
    }
}

// ─────────────────────────────────────────────
// StackOverflowService — Facade
// ─────────────────────────────────────────────
public class StackOverflowService : ISearch
{
    private readonly ConcurrentDictionary<string, Member> _members = new();
    private readonly ConcurrentDictionary<string, Question> _questions = new();
    private readonly ConcurrentDictionary<string, Tag> _tags = new();

    // ── Registration ──

    public Member RegisterMember(string id, string password, string name, string email, string phone)
    {
        var account = new Account(id, password, name, email, phone);
        var member = new Member(account);
        _members.TryAdd(id, member);
        return member;
    }

    public Admin RegisterAdmin(string id, string password, string name, string email, string phone)
    {
        var account = new Account(id, password, name, email, phone);
        var admin = new Admin(account);
        _members.TryAdd(id, admin);
        return admin;
    }

    public Moderator RegisterModerator(string id, string password, string name, string email, string phone)
    {
        var account = new Account(id, password, name, email, phone);
        var mod = new Moderator(account);
        _members.TryAdd(id, mod);
        return mod;
    }

    // ── Questions ──

    public Question? PostQuestion(string memberId, string title, string description, List<string> tagNames)
    {
        if (!_members.TryGetValue(memberId, out var member)) return null;
        if (member.Account.Status != AccountStatus.Active) return null;

        // Resolve/create tags
        var tags = new List<Tag>();
        foreach (var name in tagNames)
        {
            var tag = _tags.GetOrAdd(name, _ => new Tag(name));
            tag.DailyAskedFrequency++;
            tag.WeeklyAskedFrequency++;
            tags.Add(tag);
        }

        var question = new Question(memberId, title, description, tags);
        _questions.TryAdd(question.Id, question);

        Console.WriteLine($"    [Q] {member.Account.Name} posted: \"{title}\" [{string.Join(", ", tagNames)}]");
        return question;
    }

    // ── Answers ──

    public Answer? PostAnswer(string memberId, string questionId, string answerText)
    {
        if (!_members.TryGetValue(memberId, out var member)) return null;
        if (member.Account.Status != AccountStatus.Active) return null;
        if (!_questions.TryGetValue(questionId, out var question)) return null;
        if (question.Status != QuestionStatus.Open) return null;

        var answer = new Answer(memberId, answerText);
        question.AddAnswer(answer);

        // Notify question author
        if (_members.TryGetValue(question.AuthorId, out var author))
            author.AddNotification(new Notification($"New answer on \"{question.Title}\" by {member.Account.Name}"));

        Console.WriteLine($"    [A] {member.Account.Name} answered: \"{question.Title}\"");
        return answer;
    }

    // ── Accept Answer ──

    public bool AcceptAnswer(string memberId, string questionId, string answerId)
    {
        if (!_questions.TryGetValue(questionId, out var question)) return false;
        if (question.AuthorId != memberId) return false;

        var answer = question.Answers.FirstOrDefault(a => a.Id == answerId);
        if (answer == null) return false;

        answer.Accepted = true;
        // +15 rep to answer author
        if (_members.TryGetValue(answer.AuthorId, out var answerAuthor))
            answerAuthor.Account.Reputation += 15;

        Console.WriteLine($"    [✓] Accepted answer by {answer.AuthorId} on \"{question.Title}\"");
        return true;
    }

    // ── Comments ──

    public Comment? CommentOnQuestion(string memberId, string questionId, string text)
    {
        if (!_members.TryGetValue(memberId, out var member)) return null;
        if (!_questions.TryGetValue(questionId, out var question)) return null;

        var comment = new Comment(text);
        question.AddComment(comment);
        return comment;
    }

    public Comment? CommentOnAnswer(string memberId, string questionId, string answerId, string text)
    {
        if (!_members.TryGetValue(memberId, out _)) return null;
        if (!_questions.TryGetValue(questionId, out var question)) return null;
        var answer = question.Answers.FirstOrDefault(a => a.Id == answerId);
        if (answer == null) return null;

        var comment = new Comment(text);
        answer.AddComment(comment);
        return comment;
    }

    // ── Voting ──

    public bool VoteQuestion(string memberId, string questionId)
    {
        if (!_members.TryGetValue(memberId, out _)) return false;
        if (!_questions.TryGetValue(questionId, out var question)) return false;

        question.IncrementVoteCount();
        // +10 rep to author
        if (_members.TryGetValue(question.AuthorId, out var author))
            author.Account.Reputation += 10;
        return true;
    }

    public bool VoteAnswer(string memberId, string questionId, string answerId)
    {
        if (!_members.TryGetValue(memberId, out _)) return false;
        if (!_questions.TryGetValue(questionId, out var question)) return false;
        var answer = question.Answers.FirstOrDefault(a => a.Id == answerId);
        if (answer == null) return false;

        answer.IncrementVoteCount();
        if (_members.TryGetValue(answer.AuthorId, out var author))
            author.Account.Reputation += 10;
        return true;
    }

    public bool VoteComment(string memberId, string questionId, string commentId)
    {
        if (!_members.TryGetValue(memberId, out _)) return false;
        if (!_questions.TryGetValue(questionId, out var question)) return false;
        var comment = question.Comments.FirstOrDefault(c => c.Id == commentId);
        if (comment != null) { comment.IncrementVoteCount(); return true; }
        return false;
    }

    // ── Flagging ──

    public bool FlagQuestion(string memberId, string questionId)
    {
        if (!_questions.TryGetValue(questionId, out var question)) return false;
        // Simplified: just increment view as "flagged" indicator
        Console.WriteLine($"    [Flag] Question \"{question.Title}\" flagged by {memberId}");
        return true;
    }

    public bool FlagAnswer(string memberId, string questionId, string answerId)
    {
        if (!_questions.TryGetValue(questionId, out var question)) return false;
        var answer = question.Answers.FirstOrDefault(a => a.Id == answerId);
        if (answer == null) return false;
        answer.IncrementFlagCount();
        Console.WriteLine($"    [Flag] Answer by {answer.AuthorId} flagged by {memberId}");
        return true;
    }

    // ── Bounty ──

    public bool AddBounty(string memberId, string questionId, int reputation)
    {
        if (!_members.TryGetValue(memberId, out var member)) return false;
        if (!_questions.TryGetValue(questionId, out var question)) return false;
        if (question.AuthorId != memberId) return false;
        if (member.Account.Reputation < reputation) return false;

        var bounty = new Bounty(reputation);
        bounty.ModifyReputation(member.Account);
        question.Bounty = bounty;
        Console.WriteLine($"    [Bounty] {member.Account.Name} added {reputation} rep bounty to \"{question.Title}\"");
        return true;
    }

    // ── Search (ISearch) ──

    public List<Question> Search(string query)
    {
        return _questions.Values
            .Where(q => q.Status != QuestionStatus.Deleted &&
                (q.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 q.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 q.Tags.Any(t => t.Name.Equals(query, StringComparison.OrdinalIgnoreCase))))
            .ToList();
    }

    // ── Tags: Most Frequently Used ──

    public List<Tag> GetTopTags(int count = 10) =>
        _tags.Values.OrderByDescending(t => t.DailyAskedFrequency).Take(count).ToList();

    // ── Badges ──

    public void AwardBadge(string memberId, string badgeName, string description)
    {
        if (_members.TryGetValue(memberId, out var member))
        {
            member.CollectBadge(new Badge(badgeName, description));
            Console.WriteLine($"    [Badge] {member.Account.Name} earned \"{badgeName}\"");
        }
    }

    // ── Helpers ──

    public Member? GetMember(string id) => _members.TryGetValue(id, out var m) ? m : null;
    public Question? GetQuestion(string id) => _questions.TryGetValue(id, out var q) ? q : null;
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var service = new StackOverflowService();

        // Register users with different roles
        var alice = service.RegisterMember("alice", "pass", "Alice", "alice@mail.com", "123");
        var bob = service.RegisterMember("bob", "pass", "Bob", "bob@mail.com", "456");
        var charlie = service.RegisterMember("charlie", "pass", "Charlie", "charlie@mail.com", "789");
        var mod = service.RegisterModerator("mod1", "pass", "ModDave", "mod@mail.com", "000");
        var admin = service.RegisterAdmin("admin1", "pass", "AdminEve", "admin@mail.com", "111");

        // Give Alice rep for bounty
        alice.Account.Reputation = 100;

        // ── Post Question ──
        Console.WriteLine("=== Post Question ===\n");
        var q1 = service.PostQuestion("alice", "How to reverse a linked list?",
            "Need efficient approach in C#",
            new List<string> { "c#", "linked-list", "algorithms" });

        // ── Post Answers ──
        Console.WriteLine("\n=== Post Answers ===\n");
        var a1 = service.PostAnswer("bob", q1!.Id, "Iterative with three pointers");
        var a2 = service.PostAnswer("charlie", q1.Id, "Recursive approach");

        // ── Vote ──
        Console.WriteLine("\n=== Voting ===\n");
        service.VoteQuestion("bob", q1.Id);
        service.VoteQuestion("charlie", q1.Id);
        service.VoteAnswer("alice", q1.Id, a1!.Id);
        service.VoteAnswer("alice", q1.Id, a2!.Id);
        Console.WriteLine($"    Q votes: {q1.VoteCount}, Alice rep: {alice.Account.Reputation}");
        Console.WriteLine($"    A1 votes: {a1.VoteCount}, Bob rep: {bob.Account.Reputation}");

        // ── Comments ──
        Console.WriteLine("\n=== Comments ===\n");
        var c1 = service.CommentOnQuestion("bob", q1.Id, "Have you tried iterative first?");
        service.CommentOnAnswer("alice", q1.Id, a1.Id, "Thanks! Very clear.");
        Console.WriteLine($"    Q comments: {q1.Comments.Count}, A1 comments: {a1.Comments.Count}");

        // ── Accept Answer ──
        Console.WriteLine("\n=== Accept Answer ===\n");
        service.AcceptAnswer("alice", q1.Id, a1.Id);
        Console.WriteLine($"    Bob rep after accept: {bob.Account.Reputation}");

        // ── Bounty ──
        Console.WriteLine("\n=== Bounty ===\n");
        service.AddBounty("alice", q1.Id, 50);
        Console.WriteLine($"    Alice rep after bounty: {alice.Account.Reputation}");

        // ── Moderator: Close + Undelete ──
        Console.WriteLine("\n=== Moderator Actions ===\n");
        var q2 = service.PostQuestion("bob", "Favorite color?", "Just curious", new List<string> { "off-topic" });
        mod.CloseQuestion(q2!, QuestionClosingRemark.OffTopic);
        Console.WriteLine($"    Q2 status: {q2!.Status}, remark: {q2.ClosingRemark}");

        q2.Status = QuestionStatus.Deleted; // simulate delete
        mod.UndeleteQuestion(q2);
        Console.WriteLine($"    Q2 after undelete: {q2.Status}");

        // ── Admin: Block/Unblock ──
        Console.WriteLine("\n=== Admin Actions ===\n");
        admin.BlockMember(charlie);
        Console.WriteLine($"    Charlie status: {charlie.Account.Status}");
        var blockedPost = service.PostQuestion("charlie", "Can I post?", "Testing", new List<string> { "test" });
        Console.WriteLine($"    Blocked charlie post: {(blockedPost != null ? "SUCCESS" : "BLOCKED")}");
        admin.UnblockMember(charlie);
        Console.WriteLine($"    Charlie status after unblock: {charlie.Account.Status}");

        // ── Search + Tags ──
        Console.WriteLine("\n=== Search + Tags ===\n");
        var results = service.Search("linked list");
        Console.WriteLine($"    Search 'linked list': {results.Count} results");

        var topTags = service.GetTopTags(5);
        Console.WriteLine($"    Top tags: {string.Join(", ", topTags.Select(t => t.ToString()))}");

        // ── Badges ──
        Console.WriteLine("\n=== Badges ===\n");
        service.AwardBadge("bob", "First Answer", "Posted first answer");
        service.AwardBadge("alice", "Curious", "Asked first question");
        Console.WriteLine($"    Bob badges: [{string.Join(", ", bob.Badges)}]");
        Console.WriteLine($"    Alice badges: [{string.Join(", ", alice.Badges)}]");

        // ── Mark Favorite ──
        Console.WriteLine("\n=== Favorites ===\n");
        bob.MarkFavorite(q1.Id);
        Console.WriteLine($"    Bob favorites: {bob.Favorites.Count}");

        // ── Flag ──
        Console.WriteLine("\n=== Flagging ===\n");
        service.FlagAnswer("alice", q1.Id, a2!.Id);
        Console.WriteLine($"    A2 flag count: {a2.FlagCount}");
    }
}
