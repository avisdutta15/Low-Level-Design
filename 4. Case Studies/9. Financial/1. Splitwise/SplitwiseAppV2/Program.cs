// Splitwise Application V2 — Simplified Design with Global Ledger
//
// Problem:
//   Same as V1 (expense sharing among groups) PLUS support for 1:1 expenses
//   between any two people without requiring a group.
//
// Key Differences from V1:
//   - ONE global balance ledger instead of per-user-per-group BalanceSheets
//   - 1:1 expenses work without any group (Alice paid for Bob's coffee — done)
//   - Group expenses also update the SAME global ledger
//   - Debt simplification works across ALL debts (1:1 + group combined)
//   - No separate BalanceSheet/BalanceSheetService/ExpenseService/GroupRepo classes
//   - ONE service class: SplitwiseService (facade for everything)
//
// Architecture:
//   Client → SplitwiseService (single facade)
//                ├─► _users: Dictionary<string, User>
//                ├─► _groups: Dictionary<string, Group> (optional, for organizing)
//                ├─► _expenses: List<Expense> (history only, not used for balance calc)
//                ├─► _ledger: Dictionary<(string, string), double>  ← THE source of truth
//                └─► SplitStrategyFactory → ISplitStrategy (Equal / Percentage)
//
// The Global Ledger:
//   - Key: (userIdA, userIdB) where A < B alphabetically (normalized)
//   - Value: positive = A owes B, negative = B owes A
//   - Every expense (1:1 or group) updates THIS ledger
//   - SimplifyDebts reads THIS ledger to compute minimal settlements
//   - SettleUp also updates THIS ledger (reduces debt)
//
// Why Global Ledger (not per-group):
//   - In real Splitwise, the debt is between TWO PEOPLE, not between a person and a group
//   - If Alice owes Bob $50 from "Roommates" and $20 from "Trip", the net is $70 — one payment
//   - Per-group balances would show two separate $50 and $20 debts, requiring two settlements
//   - Global ledger naturally merges all debts into one net balance per pair
//
// Design Patterns:
//   - Strategy Pattern: ISplitStrategy (Equal, Percentage) — pluggable via factory
//   - Factory Pattern: SplitStrategyFactory maps SplitType → strategy
//   - Facade Pattern: SplitwiseService is the ONLY thing the client talks to
//
// API:
//   service.AddUser(id, name, email)                              — register a user
//   service.CreateGroup(name, members)                            — create a group
//   service.AddExpense(paidBy, owes, amount, description)         — 1:1 expense (no group)
//   service.AddGroupExpense(groupId, paidBy, amount, splitType, participants, details)  — group expense
//   service.PrintBalances()                                       — show all pairwise debts
//   service.SimplifyDebts()                                       — minimize settlement transactions
//   service.SettleUp(from, to, amount)                            — partial/full payment
//
// Split Flow:
//   1. Client calls AddGroupExpense (or AddExpense for 1:1)
//   2. Strategy.Split() computes each participant's share
//   3. For each participant != paidBy: UpdateLedger(participant, paidBy, shareAmount)
//   4. Ledger key is normalized (alphabetical) so (Alice,Bob) and (Bob,Alice) map to same entry
//
// Simplification Flow:
//   1. Read all non-zero entries from _ledger
//   2. Compute net balance per user (sum their side of each ledger entry)
//   3. Positive net = owed money (giver), Negative net = owes money (receiver)
//   4. Greedy heap: match biggest giver + biggest receiver, settle min, repeat

// ─────────────────────────────────────────────
// Enums + Models
// ─────────────────────────────────────────────
public enum SplitType { EQUAL, PERCENTAGE }

public class User
{
    public string Id { get; }
    public string Name { get; }
    public string Email { get; }

    public User(string id, string name, string email) { Id = id; Name = name; Email = email; }
    public override string ToString() => Name;
}

public class Group
{
    public string Id { get; }
    public string Name { get; }
    public List<User> Members { get; } = new();

    public Group(string id, string name) { Id = id; Name = name; }
    public void AddMember(User u) { if (!Members.Contains(u)) Members.Add(u); }
}

public class Expense
{
    public string Id { get; }
    public string Description { get; }
    public double Amount { get; }
    public User PaidBy { get; }
    public List<(User user, double amount)> Splits { get; }
    public string? GroupId { get; } // null = 1:1 expense

    public Expense(string desc, double amount, User paidBy, List<(User, double)> splits, string? groupId = null)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Description = desc; Amount = amount; PaidBy = paidBy; Splits = splits; GroupId = groupId;
    }
}

// ─────────────────────────────────────────────
// Split Strategy (same pattern, cleaner)
// ─────────────────────────────────────────────
public interface ISplitStrategy
{
    List<(User user, double amount)> Split(double amount, List<User> participants, Dictionary<string, double>? details);
}

public class EqualSplitStrategy : ISplitStrategy
{
    public List<(User, double)> Split(double amount, List<User> participants, Dictionary<string, double>? details)
    {
        var result = new List<(User, double)>();
        int count = participants.Count;
        double perPerson = Math.Floor(amount * 100 / count) / 100;
        double remainder = Math.Round(amount - (perPerson * count), 2);

        for (int i = 0; i < count; i++)
        {
            double share = perPerson;
            if (i == 0) share += remainder; // first absorbs rounding
            result.Add((participants[i], share));
        }
        return result;
    }
}

public class PercentageSplitStrategy : ISplitStrategy
{
    public List<(User, double)> Split(double amount, List<User> participants, Dictionary<string, double>? details)
    {
        var result = new List<(User, double)>();
        if (details == null) return result;

        for (int i = 0; i < participants.Count; i++)
        {
            double pct = details.ContainsKey(participants[i].Id) ? details[participants[i].Id] : 0;
            result.Add((participants[i], Math.Round(amount * pct / 100, 2)));
        }
        return result;
    }
}

public static class SplitStrategyFactory
{
    public static ISplitStrategy GetStrategy(SplitType type)
    {
        if (type == SplitType.EQUAL) return new EqualSplitStrategy();
        return new PercentageSplitStrategy();
    }
}

// ─────────────────────────────────────────────
// SplitwiseService — single facade, global ledger
// ─────────────────────────────────────────────
public class SplitwiseService
{
    private readonly Dictionary<string, User> _users = new();
    private readonly Dictionary<string, Group> _groups = new();
    private readonly List<Expense> _expenses = new();

    // THE global balance ledger: (owerId, owedId) → amount owed
    // Positive = ower owes owed that amount
    // We normalize so only one direction exists per pair
    private readonly Dictionary<(string, string), double> _ledger = new();

    // ── Users ──

    public User AddUser(string id, string name, string email)
    {
        var user = new User(id, name, email);
        _users[id] = user;
        return user;
    }

    // ── Groups ──

    public Group CreateGroup(string name, List<User> members)
    {
        var group = new Group(Guid.NewGuid().ToString("N")[..8], name);
        for (int i = 0; i < members.Count; i++)
            group.AddMember(members[i]);
        _groups[group.Id] = group;
        Console.WriteLine($"    [Group] Created \"{name}\" ({members.Count} members)");
        return group;
    }

    // ── 1:1 Expense (no group needed) ──

    public void AddExpense(User paidBy, User owes, double amount, string description = "")
    {
        if (paidBy.Id == owes.Id) return;

        // Record expense for history
        var splits = new List<(User, double)> { (owes, amount) };
        _expenses.Add(new Expense(description, amount, paidBy, splits));

        // Update global ledger: owes owes paidBy this amount
        UpdateLedger(owes.Id, paidBy.Id, amount);

        Console.WriteLine($"    [1:1] {paidBy.Name} paid ${amount:F2} for {owes.Name}" +
            (description != "" ? $" ({description})" : ""));
    }

    // ── Group Expense (split among participants) ──

    public void AddGroupExpense(string groupId, User paidBy, double amount, SplitType splitType,
        List<User>? participants = null, Dictionary<string, double>? splitDetails = null, string description = "")
    {
        if (!_groups.ContainsKey(groupId)) return;
        var group = _groups[groupId];

        // Default: split among all group members
        var splitParticipants = participants ?? group.Members;

        var strategy = SplitStrategyFactory.GetStrategy(splitType);
        var splits = strategy.Split(amount, splitParticipants, splitDetails);

        // Record expense
        _expenses.Add(new Expense(description, amount, paidBy, splits, groupId));

        // Update ledger for each split participant (except the payer)
        for (int i = 0; i < splits.Count; i++)
        {
            if (splits[i].user.Id == paidBy.Id) continue;
            UpdateLedger(splits[i].user.Id, paidBy.Id, splits[i].amount);
        }

        Console.WriteLine($"    [Group] {paidBy.Name} paid ${amount:F2} ({splitType}) in \"{group.Name}\"");
        for (int i = 0; i < splits.Count; i++)
            if (splits[i].user.Id != paidBy.Id)
                Console.WriteLine($"      {splits[i].user.Name} owes ${splits[i].amount:F2}");
    }

    // ── View Balances ──

    public void PrintBalances()
    {
        Console.WriteLine("    === All Balances ===");
        foreach (var entry in _ledger)
        {
            if (Math.Abs(entry.Value) < 0.01) continue;
            string owerName = _users.ContainsKey(entry.Key.Item1) ? _users[entry.Key.Item1].Name : entry.Key.Item1;
            string owedName = _users.ContainsKey(entry.Key.Item2) ? _users[entry.Key.Item2].Name : entry.Key.Item2;

            if (entry.Value > 0)
                Console.WriteLine($"      {owerName} owes {owedName} ${entry.Value:F2}");
            else
                Console.WriteLine($"      {owedName} owes {owerName} ${Math.Abs(entry.Value):F2}");
        }
    }

    // ── Settle Up (partial or full) ──

    public void SettleUp(User from, User to, double amount)
    {
        // from pays to → reduces what from owes to
        UpdateLedger(from.Id, to.Id, -amount);
        Console.WriteLine($"    [Settle] {from.Name} paid {to.Name} ${amount:F2}");
    }

    // ── Simplify Debts (greedy heap on global ledger) ──

    public void SimplifyDebts()
    {
        // Calculate net balance per user from the ledger
        var netBalances = new Dictionary<string, double>();
        foreach (var entry in _ledger)
        {
            if (Math.Abs(entry.Value) < 0.01) continue;

            string ower = entry.Key.Item1;
            string owed = entry.Key.Item2;
            double amount = entry.Value;

            if (!netBalances.ContainsKey(ower)) netBalances[ower] = 0;
            if (!netBalances.ContainsKey(owed)) netBalances[owed] = 0;

            if (amount > 0)
            {
                netBalances[ower] -= amount; // ower owes (net negative)
                netBalances[owed] += amount; // owed is owed (net positive)
            }
            else
            {
                netBalances[owed] -= Math.Abs(amount);
                netBalances[ower] += Math.Abs(amount);
            }
        }

        // Givers (positive net = owed money) and Receivers (negative net = owe money)
        var givers = new PriorityQueue<(string id, double amount), double>(); // min-heap
        var receivers = new PriorityQueue<(string id, double amount), double>(new MaxComp()); // max-heap

        foreach (var entry in netBalances)
        {
            if (entry.Value > 0.01)
                givers.Enqueue((entry.Key, entry.Value), -entry.Value);
            else if (entry.Value < -0.01)
                receivers.Enqueue((entry.Key, Math.Abs(entry.Value)), Math.Abs(entry.Value));
        }

        Console.WriteLine("    === Simplified Settlements ===");
        while (givers.Count > 0 && receivers.Count > 0)
        {
            var giver = givers.Dequeue();
            var receiver = receivers.Dequeue();

            double settled = Math.Min(giver.amount, receiver.amount);
            string receiverName = _users.ContainsKey(receiver.id) ? _users[receiver.id].Name : receiver.id;
            string giverName = _users.ContainsKey(giver.id) ? _users[giver.id].Name : giver.id;

            Console.WriteLine($"      {receiverName} pays {giverName} ${settled:F2}");

            double giverRemain = giver.amount - settled;
            double receiverRemain = receiver.amount - settled;

            if (giverRemain > 0.01) givers.Enqueue((giver.id, giverRemain), -giverRemain);
            if (receiverRemain > 0.01) receivers.Enqueue((receiver.id, receiverRemain), receiverRemain);
        }
    }

    // ── Internal: update the global ledger ──
    // Normalizes key so only one direction exists per pair (alphabetical order)
    private void UpdateLedger(string owerId, string owedId, double amount)
    {
        // Always store with smaller ID first for consistency
        var key = String.Compare(owerId, owedId) < 0 ? (owerId, owedId) : (owedId, owerId);

        if (!_ledger.ContainsKey(key))
            _ledger[key] = 0;

        // If key is (ower, owed) in natural order, positive = ower owes owed
        // If we flipped, we negate
        if (String.Compare(owerId, owedId) < 0)
            _ledger[key] += amount; // natural order: owerId owes owedId
        else
            _ledger[key] -= amount; // flipped: owedId is first in key, so negate
    }
}

public class MaxComp : IComparer<double>
{
    public int Compare(double a, double b) { return b.CompareTo(a); }
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var service = new SplitwiseService();

        var alice = service.AddUser("u1", "Alice", "alice@mail.com");
        var bob = service.AddUser("u2", "Bob", "bob@mail.com");
        var charlie = service.AddUser("u3", "Charlie", "charlie@mail.com");
        var dave = service.AddUser("u4", "Dave", "dave@mail.com");

        // ── 1:1 Expenses (no group) ──
        Console.WriteLine("=== 1:1 Expenses (no group needed) ===\n");
        service.AddExpense(alice, bob, 50, "Coffee");        // Alice paid for Bob's coffee
        service.AddExpense(bob, charlie, 30, "Lunch");       // Bob paid for Charlie's lunch
        service.AddExpense(charlie, alice, 20, "Snacks");    // Charlie paid for Alice's snacks

        Console.WriteLine();
        service.PrintBalances();

        // ── Group Expense (equal split) ──
        Console.WriteLine("\n=== Group Expense: Roommates ===\n");
        var roommates = service.CreateGroup("Roommates", new List<User> { alice, bob, charlie, dave });

        service.AddGroupExpense(roommates.Id, alice, 200, SplitType.EQUAL,
            description: "Groceries");

        service.AddGroupExpense(roommates.Id, dave, 120, SplitType.EQUAL,
            description: "Utilities");

        // ── Group Expense (percentage split) ──
        Console.WriteLine("\n=== Group Expense: Percentage Split (Rent) ===\n");
        service.AddGroupExpense(roommates.Id, charlie, 2000, SplitType.PERCENTAGE,
            splitDetails: new Dictionary<string, double> { { "u1", 30 }, { "u2", 25 }, { "u3", 25 }, { "u4", 20 } },
            description: "Rent");

        // ── Mix: another 1:1 ──
        Console.WriteLine("\n=== More 1:1 ===\n");
        service.AddExpense(dave, bob, 40, "Uber ride");

        // ── All balances ──
        Console.WriteLine("\n=== All Balances (1:1 + group combined) ===\n");
        service.PrintBalances();

        // ── Simplify everything ──
        Console.WriteLine("\n=== Simplified (all debts across all contexts) ===\n");
        service.SimplifyDebts();

        // ── Settle up partially ──
        Console.WriteLine("\n=== Partial Settlement ===\n");
        service.SettleUp(bob, alice, 50); // Bob pays Alice $50

        Console.WriteLine("\n=== Balances After Settlement ===\n");
        service.PrintBalances();

        Console.WriteLine("\n=== Simplified After Settlement ===\n");
        service.SimplifyDebts();
    }
}
