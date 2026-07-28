// Splitwise Application V1 — Group-Based Expense Sharing
//
// Problem:
//   Groups of people (roommates, friends, travelers) share expenses.
//   Rather than settling after each expense, the system tracks running balances
//   and can simplify complex debt graphs into minimal transactions.
//
// Architecture:
//   Client → GroupService (Facade)
//                ├─► GroupRepo (stores groups)
//                ├─► ExpenseService (creates expenses, applies splits)
//                │       ├─► SplitStrategyFactory → ISplitStrategy (Equal / Percentage)
//                │       └─► BalanceSheetService (updates pairwise balances per group)
//                └─► SettleUpService (simplifies debts using greedy heap algorithm)
//
// Design Patterns:
//   - Strategy Pattern: ISplitStrategy (Equal, Percentage) — pluggable split logic
//   - Factory Pattern: SplitStrategyFactory maps SplitType enum → strategy instance
//   - Repository Pattern: GroupRepo stores/retrieves groups
//   - Facade Pattern: GroupService hides internal services from the client
//
// Key Design Decisions:
//   - BalanceSheet is PER USER PER GROUP — tracks pairwise debts within that group
//   - BalanceSheet.Balances: Map<User, double> — positive = they owe me, negative = I owe them
//   - Rounding in equal splits: first person absorbs the fractional cent difference
//   - Debt simplification: greedy algorithm with max-heaps (same as SplitWiseAlgorithm project)
//
// Limitation:
//   - All expenses MUST belong to a group (no 1:1 expenses without a group)
//   - Fixed in V2 which introduces a global balance ledger
//
// Entities:
//   User         — id, name, email, phone
//   Group        — id, name, members, expenses, balanceSheets (per user)
//   Expense      — id, description, amount, paidBy, splits, splitType
//   Split        — user + amount (how much that user owes for this expense)
//   BalanceSheet — totalOwed, totalOwing, balances map (per-user-per-group)
//
// Services:
//   GroupService         — client-facing facade (createGroup, addExpense, simplifyDebts)
//   ExpenseService       — creates expenses, delegates to strategy + balance service
//   BalanceSheetService  — updates pairwise balances after an expense
//   SettleUpService      — simplifies debts using greedy heap algorithm
//
// Split Flow:
//   1. Client calls GroupService.AddExpense(groupId, paidBy, amount, splitType, participants)
//   2. ExpenseService asks SplitStrategyFactory for the correct strategy
//   3. Strategy.Split() returns list of (user, amount) splits
//   4. BalanceSheetService.UpdateBalances() updates each participant's pairwise balance with paidBy
//   5. The expense is stored in group.Expenses for history
//
// Simplification Flow:
//   1. Client calls GroupService.SimplifyDebts(groupId)
//   2. SettleUpService calculates net balance per user (sum of all their pairwise balances)
//   3. Separates into givers (net positive = owed money) and receivers (net negative = owe money)
//   4. Greedy: match biggest giver with biggest receiver, settle min(amounts), repeat

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────
public enum SplitType { EQUAL, PERCENTAGE }

// ─────────────────────────────────────────────
// Entities
// ─────────────────────────────────────────────
public class User
{
    public string Id { get; }
    public string Name { get; }
    public string Email { get; }
    public string Phone { get; }

    public User(string id, string name, string email, string phone = "")
    {
        Id = id; Name = name; Email = email; Phone = phone;
    }

    public override string ToString() => Name;
    public override int GetHashCode() => Id.GetHashCode();
    public override bool Equals(object? obj) => obj is User u && u.Id == Id;
}

public class Split
{
    public User User { get; }
    public double Amount { get; }

    public Split(User user, double amount) { User = user; Amount = amount; }
    public override string ToString() => $"{User.Name} owes ${Amount:F2}";
}

public class Expense
{
    public string Id { get; }
    public string Description { get; }
    public double Amount { get; }
    public User PaidBy { get; }
    public List<Split> Splits { get; }
    public SplitType SplitType { get; }

    public Expense(string description, double amount, User paidBy, List<Split> splits, SplitType splitType)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Description = description; Amount = amount; PaidBy = paidBy;
        Splits = splits; SplitType = splitType;
    }

    public override string ToString() => $"Expense(\"{Description}\", ${Amount:F2}, paid by {PaidBy.Name})";
}

public class BalanceSheet
{
    public double TotalOwed { get; set; }    // how much this user is owed by others
    public double TotalOwing { get; set; }   // how much this user owes to others
    public Dictionary<User, double> Balances { get; } = new(); // positive = they owe you, negative = you owe them

    // Update balance with another user
    public void UpdateBalance(User other, double amount)
    {
        if (!Balances.ContainsKey(other))
            Balances[other] = 0;
        Balances[other] += amount;
    }
}

public class Group
{
    public string Id { get; }
    public string Name { get; }
    public List<User> Members { get; } = new();
    public List<Expense> Expenses { get; } = new();
    public Dictionary<User, BalanceSheet> BalanceSheets { get; } = new();

    public Group(string id, string name) { Id = id; Name = name; }

    public void AddMember(User user)
    {
        if (!Members.Contains(user))
        {
            Members.Add(user);
            BalanceSheets[user] = new BalanceSheet();
        }
    }

    public override string ToString() => $"Group(\"{Name}\", {Members.Count} members)";
}

// ─────────────────────────────────────────────
// Split Strategy (Strategy Pattern)
// ─────────────────────────────────────────────
public interface ISplitStrategy
{
    List<Split> Split(double amount, User paidBy, List<User> participants, Dictionary<string, double>? splitDetails);
}

// Equal split: divide equally among all participants
// Handles rounding: first person absorbs the rounding difference
public class EqualSplitStrategy : ISplitStrategy
{
    public List<Split> Split(double amount, User paidBy, List<User> participants, Dictionary<string, double>? splitDetails)
    {
        var splits = new List<Split>();
        int count = participants.Count;
        double perPerson = Math.Floor(amount * 100 / count) / 100; // round down to 2 decimals
        double remainder = amount - (perPerson * count);

        for (int i = 0; i < count; i++)
        {
            double share = perPerson;
            if (i == 0) share += remainder; // first person absorbs rounding difference
            splits.Add(new Split(participants[i], share));
        }
        return splits;
    }
}

// Percentage split: each participant pays a % of the total
// splitDetails: userId → percentage (must sum to 100)
public class PercentageSplitStrategy : ISplitStrategy
{
    public List<Split> Split(double amount, User paidBy, List<User> participants, Dictionary<string, double>? splitDetails)
    {
        var splits = new List<Split>();
        if (splitDetails == null) return splits;

        double totalPercent = 0;
        for (int i = 0; i < participants.Count; i++)
        {
            if (splitDetails.ContainsKey(participants[i].Id))
                totalPercent += splitDetails[participants[i].Id];
        }

        // Validate percentages sum to ~100
        if (Math.Abs(totalPercent - 100) > 0.01)
        {
            Console.WriteLine($"    [Error] Percentages sum to {totalPercent}, must be 100");
            return splits;
        }

        for (int i = 0; i < participants.Count; i++)
        {
            double percent = splitDetails.ContainsKey(participants[i].Id) ? splitDetails[participants[i].Id] : 0;
            double share = Math.Round(amount * percent / 100, 2);
            splits.Add(new Split(participants[i], share));
        }
        return splits;
    }
}

// Factory: creates strategy from enum
public static class SplitStrategyFactory
{
    public static ISplitStrategy GetStrategy(SplitType type)
    {
        if (type == SplitType.EQUAL) return new EqualSplitStrategy();
        else if (type == SplitType.PERCENTAGE) return new PercentageSplitStrategy();
        else throw new ArgumentException($"Unknown split type: {type}");
    }
}

// ─────────────────────────────────────────────
// Services
// ─────────────────────────────────────────────

// BalanceSheetService: updates pairwise balances after an expense
public class BalanceSheetService
{
    // After an expense: paidBy is owed by each split participant
    public void UpdateBalances(Group group, User paidBy, List<Split> splits)
    {
        for (int i = 0; i < splits.Count; i++)
        {
            var split = splits[i];
            if (split.User.Id == paidBy.Id) continue; // don't owe yourself

            double amount = split.Amount;

            // paidBy is owed 'amount' by split.User
            group.BalanceSheets[paidBy].UpdateBalance(split.User, amount);
            group.BalanceSheets[paidBy].TotalOwed += amount;

            // split.User owes 'amount' to paidBy
            group.BalanceSheets[split.User].UpdateBalance(paidBy, -amount);
            group.BalanceSheets[split.User].TotalOwing += amount;
        }
    }

    // Print all non-zero balances in a group
    public void PrintGroupBalances(Group group)
    {
        Console.WriteLine($"    === Balances for \"{group.Name}\" ===");
        for (int i = 0; i < group.Members.Count; i++)
        {
            var member = group.Members[i];
            var sheet = group.BalanceSheets[member];
            foreach (var entry in sheet.Balances)
            {
                if (Math.Abs(entry.Value) > 0.01)
                {
                    if (entry.Value > 0)
                        Console.WriteLine($"      {entry.Key.Name} owes {member.Name} ${entry.Value:F2}");
                    else
                        Console.WriteLine($"      {member.Name} owes {entry.Key.Name} ${Math.Abs(entry.Value):F2}");
                }
            }
        }
    }
}

// ExpenseService: creates expenses and updates balances
public class ExpenseService
{
    private readonly BalanceSheetService _balanceService = new();

    public void AddExpense(Group group, User paidBy, double amount, SplitType splitType,
        List<User> participants, Dictionary<string, double>? splitDetails = null)
    {
        var strategy = SplitStrategyFactory.GetStrategy(splitType);
        var splits = strategy.Split(amount, paidBy, participants, splitDetails);

        if (splits.Count == 0) return;

        var expense = new Expense($"Expense ${amount:F2}", amount, paidBy, splits, splitType);
        group.Expenses.Add(expense);

        // Update balances
        _balanceService.UpdateBalances(group, paidBy, splits);

        Console.WriteLine($"    [Expense] {paidBy.Name} paid ${amount:F2} ({splitType}) → {splits.Count} splits");
        for (int i = 0; i < splits.Count; i++)
            Console.WriteLine($"      {splits[i]}");
    }

    public BalanceSheetService GetBalanceSheetService() => _balanceService;
}

// SettleUpService: simplifies debts using greedy heap algorithm
public class SettleUpService
{
    public void SimplifyDebts(Group group)
    {
        // Calculate net balance per user across all pairwise balances
        var netBalances = new Dictionary<string, double>();
        for (int i = 0; i < group.Members.Count; i++)
        {
            var member = group.Members[i];
            double net = 0;
            var sheet = group.BalanceSheets[member];
            foreach (var entry in sheet.Balances)
                net += entry.Value; // positive = owed, negative = owes
            if (Math.Abs(net) > 0.01)
                netBalances[member.Id] = net;
        }

        // Givers (negative net = they paid more, are owed) and Receivers (positive net = they owe)
        // Wait — in BalanceSheet, positive means "they owe me", so net positive = I'm owed = giver
        // net negative = I owe others = receiver
        var givers = new PriorityQueue<(string id, double amount), double>();  // min-heap
        var receivers = new PriorityQueue<(string id, double amount), double>(new MaxComp()); // max-heap

        foreach (var entry in netBalances)
        {
            if (entry.Value > 0.01)
            {
                // Positive = owed by others = giver (will receive in settlement)
                givers.Enqueue((entry.Key, entry.Value), -entry.Value); // min-heap: most negative priority pops first
            }
            else if (entry.Value < -0.01)
            {
                // Negative = owes others = receiver (will give in settlement)
                receivers.Enqueue((entry.Key, Math.Abs(entry.Value)), Math.Abs(entry.Value)); // max-heap
            }
        }

        Console.WriteLine($"    === Simplified Debts for \"{group.Name}\" ===");
        var settlements = new List<(string from, string to, double amount)>();

        while (givers.Count > 0 && receivers.Count > 0)
        {
            var giver = givers.Dequeue();
            var receiver = receivers.Dequeue();

            double settled = Math.Min(giver.amount, receiver.amount);
            settlements.Add((receiver.id, giver.id, settled));

            double giverRemain = giver.amount - settled;
            double receiverRemain = receiver.amount - settled;

            if (giverRemain > 0.01)
                givers.Enqueue((giver.id, giverRemain), -giverRemain);
            if (receiverRemain > 0.01)
                receivers.Enqueue((receiver.id, receiverRemain), receiverRemain);
        }

        // Find user names for display
        for (int i = 0; i < settlements.Count; i++)
        {
            var s = settlements[i];
            string fromName = "", toName = "";
            for (int j = 0; j < group.Members.Count; j++)
            {
                if (group.Members[j].Id == s.from) fromName = group.Members[j].Name;
                if (group.Members[j].Id == s.to) toName = group.Members[j].Name;
            }
            Console.WriteLine($"      {fromName} pays {toName} ${s.amount:F2}");
        }

        if (settlements.Count == 0)
            Console.WriteLine("      All settled! No payments needed.");
    }
}

// MaxComp for receivers heap
public class MaxComp : IComparer<double>
{
    public int Compare(double a, double b) { return b.CompareTo(a); }
}

// ─────────────────────────────────────────────
// GroupRepo + GroupService (Facade for client)
// ─────────────────────────────────────────────
public class GroupRepo
{
    private readonly Dictionary<string, Group> _groups = new();

    public void Save(Group group) => _groups[group.Id] = group;
    public Group? FindById(string id) => _groups.ContainsKey(id) ? _groups[id] : null;
}

// GroupService: the client-facing facade
public class GroupService
{
    private readonly GroupRepo _repo = new();
    private readonly ExpenseService _expenseService = new();
    private readonly SettleUpService _settleUpService = new();

    public Group CreateGroup(string name, List<User> members)
    {
        var group = new Group(Guid.NewGuid().ToString("N")[..8], name);
        for (int i = 0; i < members.Count; i++)
            group.AddMember(members[i]);
        _repo.Save(group);
        Console.WriteLine($"    [Group] Created \"{name}\" with {members.Count} members");
        return group;
    }

    public void AddMember(string groupId, User user)
    {
        var group = _repo.FindById(groupId);
        if (group == null) return;
        group.AddMember(user);
        Console.WriteLine($"    [Group] {user.Name} added to \"{group.Name}\"");
    }

    public void AddExpense(string groupId, User paidBy, double amount, SplitType splitType,
        List<User> participants, Dictionary<string, double>? splitDetails = null)
    {
        var group = _repo.FindById(groupId);
        if (group == null) return;
        _expenseService.AddExpense(group, paidBy, amount, splitType, participants, splitDetails);
    }

    public void SimplifyDebts(string groupId)
    {
        var group = _repo.FindById(groupId);
        if (group == null) return;
        _settleUpService.SimplifyDebts(group);
    }

    public void PrintBalances(string groupId)
    {
        var group = _repo.FindById(groupId);
        if (group == null) return;
        _expenseService.GetBalanceSheetService().PrintGroupBalances(group);
    }

    public Group? GetGroup(string id) => _repo.FindById(id);
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var service = new GroupService();

        // Create users
        var alice = new User("u1", "Alice", "alice@mail.com", "111");
        var bob = new User("u2", "Bob", "bob@mail.com", "222");
        var charlie = new User("u3", "Charlie", "charlie@mail.com", "333");
        var dave = new User("u4", "Dave", "dave@mail.com", "444");

        // Create group
        Console.WriteLine("=== Create Group ===\n");
        var group = service.CreateGroup("Roommates", new List<User> { alice, bob, charlie, dave });

        // ── Scenario 1: Equal split (dinner $120, Alice paid, split 4 ways) ──
        Console.WriteLine("\n=== Scenario 1: Equal Split ($120 dinner) ===\n");
        service.AddExpense(group.Id, alice, 120, SplitType.EQUAL,
            new List<User> { alice, bob, charlie, dave });

        // ── Scenario 2: Equal split (groceries $90, Bob paid, split 3 ways) ──
        Console.WriteLine("\n=== Scenario 2: Equal Split ($90 groceries, 3 people) ===\n");
        service.AddExpense(group.Id, bob, 90, SplitType.EQUAL,
            new List<User> { bob, charlie, dave });

        // ── Scenario 3: Percentage split (rent $2000, Charlie paid) ──
        Console.WriteLine("\n=== Scenario 3: Percentage Split ($2000 rent) ===\n");
        service.AddExpense(group.Id, charlie, 2000, SplitType.PERCENTAGE,
            new List<User> { alice, bob, charlie, dave },
            new Dictionary<string, double> { { "u1", 30 }, { "u2", 25 }, { "u3", 25 }, { "u4", 20 } });

        // ── Print pairwise balances ──
        Console.WriteLine("\n=== Pairwise Balances ===\n");
        service.PrintBalances(group.Id);

        // ── Simplify debts ──
        Console.WriteLine("\n=== Simplified Settlements ===\n");
        service.SimplifyDebts(group.Id);

        // ── Scenario 4: Add another equal expense after simplification display ──
        Console.WriteLine("\n=== Scenario 4: Another expense ($60, Dave paid, equal 4-way) ===\n");
        service.AddExpense(group.Id, dave, 60, SplitType.EQUAL,
            new List<User> { alice, bob, charlie, dave });

        Console.WriteLine("\n=== Final Simplified Settlements ===\n");
        service.SimplifyDebts(group.Id);

        // ── Scenario 5: Rounding test ($100 split 3 ways) ──
        Console.WriteLine("\n=== Scenario 5: Rounding ($100 split 3 ways) ===\n");
        var group2 = service.CreateGroup("Trip", new List<User> { alice, bob, charlie });
        service.AddExpense(group2.Id, alice, 100, SplitType.EQUAL,
            new List<User> { alice, bob, charlie });
        service.PrintBalances(group2.Id);
        Console.WriteLine();
        service.SimplifyDebts(group2.Id);
    }
}
