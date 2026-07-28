// Splitwise Debt Simplification Algorithm
//
// Problem: Given N messy transactions between people, simplify them into
// the minimum number of transactions that settle all debts.
//
// Terminology:
//   Giver   = person who GAVE money (paid for others) → net negative → will RECEIVE in settlement
//   Receiver = person who RECEIVED money (was paid for) → net positive → will GIVE in settlement
//
// Algorithm (Greedy with Heaps):
//   Phase 1: Calculate net balance for each person (incoming - outgoing)
//            Negative = giver (paid out more), Positive = receiver (received more)
//   Phase 2: Separate into givers (negative) and receivers (positive)
//            Givers heap: min-heap (most negative = biggest giver pops first)
//            Receivers heap: max-heap (largest positive = biggest receiver pops first)
//   Phase 3: Greedy settlement — match biggest giver with biggest receiver
//            Settle min(giver_amount, receiver_amount), push remainder back

// ─────────────────────────────────────────────
// Transaction: "From pays To" means From gave money on behalf of To
// ─────────────────────────────────────────────
public class Transaction
{
    public string From { get; }  // the person who GAVE (paid the bill)
    public string To { get; }    // the person who RECEIVED (was paid for)
    public double Amount { get; }

    public Transaction(string from, string to, double amount)
    {
        From = from;
        To = to;
        Amount = amount;
    }

    public override string ToString()
    {
        return From + " pays " + To + " $" + Amount.ToString("F2");
    }
}

// ─────────────────────────────────────────────
// Person: name + remaining balance to settle
// ─────────────────────────────────────────────
public class Person
{
    public string Name { get; }
    public double Balance { get; set; } // how much this person still needs to give/receive

    public Person(string name, double balance)
    {
        Name = name;
        Balance = balance;
    }
}

// ─────────────────────────────────────────────
// MaxHeapComparer: reverses comparison for receivers heap
// Default PriorityQueue = min-heap. This makes it a max-heap.
// ─────────────────────────────────────────────
public class MaxHeapComparer : IComparer<double>
{
    public int Compare(double a, double b)
    {
        return b.CompareTo(a); // reverse: largest pops first
    }
}

// ─────────────────────────────────────────────
// SplitwiseAlgorithm: simplifies debt graph
// ─────────────────────────────────────────────
public class SplitwiseAlgorithm
{
    public static List<Transaction> SimplifyDebts(List<Transaction> rawTransactions)
    {
        // Phase 1: Calculate net balance for each person
        // From (giver) gets -amount (money went OUT)
        // To (receiver) gets +amount (money came IN)
        var netBalances = new Dictionary<string, double>();

        for (int i = 0; i < rawTransactions.Count; i++)
        {
            var t = rawTransactions[i];

            if (!netBalances.ContainsKey(t.From))
                netBalances[t.From] = 0;
            if (!netBalances.ContainsKey(t.To))
                netBalances[t.To] = 0;

            netBalances[t.From] -= t.Amount; // giver: money went out (negative)
            netBalances[t.To] += t.Amount;   // receiver: money came in (positive)
        }

        // Phase 2: Separate into givers and receivers
        //
        // Givers: negative balance (e.g., -30, -10) → they gave more than they received
        //   Min-heap (default): -30 pops before -10 → biggest giver first. No trick needed.
        //
        // Receivers: positive balance (e.g., +20, +20) → they received more than they gave
        //   Max-heap (via MaxHeapComparer): +20 pops first → biggest receiver first.
        var givers = new PriorityQueue<Person, double>();                       // min-heap (default)
        var receivers = new PriorityQueue<Person, double>(new MaxHeapComparer()); // max-heap

        foreach (var entry in netBalances)
        {
            double balance = entry.Value;
            if (balance < -0.01)
            {
                // Negative → this person GAVE more than they received → they are owed
                // In settlement: they will RECEIVE money back
                var person = new Person(entry.Key, Math.Abs(balance));
                givers.Enqueue(person, balance); // priority = -30 → pops before -10 in min-heap
            }
            else if (balance > 0.01)
            {
                // Positive → this person RECEIVED more than they gave → they owe
                // In settlement: they will GIVE money back
                var person = new Person(entry.Key, balance);
                receivers.Enqueue(person, balance); // priority = 20 → pops first in max-heap
            }
            // balance ≈ 0 → this person is already settled, skip
        }

        // Phase 3: Greedy Settlement
        // Match biggest giver with biggest receiver.
        // The receiver gives money TO the giver (settling the debt).
        // Settle min(giver_amount, receiver_amount).
        // Push remainder back into the respective heap.
        var optimized = new List<Transaction>();

        while (givers.Count > 0 && receivers.Count > 0)
        {
            Person giver = givers.Dequeue();       // person who is owed the most
            Person receiver = receivers.Dequeue();  // person who owes the most

            double settledAmount = Math.Min(giver.Balance, receiver.Balance);

            // Settlement: receiver pays giver (receiver gave the money back)
            optimized.Add(new Transaction(receiver.Name, giver.Name, settledAmount));

            giver.Balance -= settledAmount;
            receiver.Balance -= settledAmount;

            // If giver still has remaining amount to receive, push back
            if (giver.Balance > 0.01)
                givers.Enqueue(giver, -giver.Balance); // negative for min-heap
            // If receiver still has remaining amount to give, push back
            if (receiver.Balance > 0.01)
                receivers.Enqueue(receiver, receiver.Balance); // positive for max-heap
        }

        return optimized;
    }
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        // Example: 5 messy, overlapping transactions
        // "Alice pays Bob $50" = Alice gave $50 on behalf of Bob
        var raw = new List<Transaction>
        {
            new Transaction("Alice", "Bob", 50),      // Alice gave for Bob
            new Transaction("Bob", "Charlie", 40),    // Bob gave for Charlie
            new Transaction("Charlie", "Alice", 20),  // Charlie gave for Alice
            new Transaction("Charlie", "David", 30),  // Charlie gave for David
            new Transaction("David", "Bob", 10)       // David gave for Bob
        };

        Console.WriteLine("--- Original Graph (5 Edges) ---");
        for (int i = 0; i < raw.Count; i++)
            Console.WriteLine("  " + raw[i]);

        Console.WriteLine();
        Console.WriteLine("--- Optimized Settlement ---");
        var optimized = SplitwiseAlgorithm.SimplifyDebts(raw);
        for (int i = 0; i < optimized.Count; i++)
            Console.WriteLine("  " + optimized[i]);

        Console.WriteLine();
        Console.WriteLine($"  Reduced: {raw.Count} transactions → {optimized.Count} transactions");

        // ── Example 2 ──
        Console.WriteLine();
        Console.WriteLine("--- Example 2: 7 Transactions ---");
        var raw2 = new List<Transaction>
        {
            new Transaction("Alice", "Bob", 100),
            new Transaction("Bob", "Charlie", 50),
            new Transaction("Charlie", "David", 30),
            new Transaction("David", "Eve", 20),
            new Transaction("Eve", "Alice", 40),
            new Transaction("Bob", "Eve", 25),
            new Transaction("Charlie", "Alice", 15)
        };

        for (int i = 0; i < raw2.Count; i++)
            Console.WriteLine("  " + raw2[i]);

        Console.WriteLine();
        Console.WriteLine("--- Optimized Settlement ---");
        var optimized2 = SplitwiseAlgorithm.SimplifyDebts(raw2);
        for (int i = 0; i < optimized2.Count; i++)
            Console.WriteLine("  " + optimized2[i]);

        Console.WriteLine($"  Reduced: {raw2.Count} → {optimized2.Count} transactions");

        // ── Assertions ──
        Console.WriteLine();
        Console.WriteLine("--- Assertions ---");
        Console.WriteLine();

        AssertSettlementCorrect(raw, optimized, "Example 1");
        AssertSettlementCorrect(raw2, optimized2, "Example 2");

        // Single transaction: A gave for B $100 → settlement: B gives A $100
        var single = new List<Transaction> { new Transaction("A", "B", 100) };
        var singleOpt = SplitwiseAlgorithm.SimplifyDebts(single);
        Assert(singleOpt.Count == 1, "Single: count == 1");
        Assert(singleOpt[0].From == "B" && singleOpt[0].To == "A" && Math.Abs(singleOpt[0].Amount - 100) < 0.01,
            "Single: B gives A $100 back");

        // Circular cancel: A gave B $50, B gave A $50 → net zero → no settlement
        var circular = new List<Transaction>
        {
            new Transaction("A", "B", 50),
            new Transaction("B", "A", 50)
        };
        Assert(SplitwiseAlgorithm.SimplifyDebts(circular).Count == 0, "Circular: 0 transactions");

        // Three-way cycle: all cancel
        var cycle = new List<Transaction>
        {
            new Transaction("A", "B", 30),
            new Transaction("B", "C", 30),
            new Transaction("C", "A", 30)
        };
        Assert(SplitwiseAlgorithm.SimplifyDebts(cycle).Count == 0, "Three-way cycle: 0 transactions");

        // Fan-out: A gave for B, C, D → in settlement B/C/D give back to A
        var fan = new List<Transaction>
        {
            new Transaction("A", "B", 10),
            new Transaction("A", "C", 20),
            new Transaction("A", "D", 30)
        };
        var fanOpt = SplitwiseAlgorithm.SimplifyDebts(fan);
        AssertSettlementCorrect(fan, fanOpt, "Fan-out");
        double totalToA = 0;
        for (int i = 0; i < fanOpt.Count; i++)
            if (fanOpt[i].To == "A") totalToA += fanOpt[i].Amount;
        Assert(Math.Abs(totalToA - 60) < 0.01, "Fan-out: A receives $60 total back");

        Console.WriteLine();
        Console.WriteLine("  All assertions passed! ✓");
    }

    // Verify: raw balance + settlement balance = 0 for every person
    // (the settlement perfectly cancels all original debts)
    static void AssertSettlementCorrect(List<Transaction> raw, List<Transaction> settlement, string testName)
    {
        var rawBal = ComputeNetBalances(raw);
        var settBal = ComputeNetBalances(settlement);

        foreach (var entry in rawBal)
        {
            double settleBalance = 0;
            if (settBal.ContainsKey(entry.Key))
                settleBalance = settBal[entry.Key];

            double sum = entry.Value + settleBalance;
            if (Math.Abs(sum) > 0.01)
            {
                Console.WriteLine($"  FAIL [{testName}]: {entry.Key} raw={entry.Value:F2} settle={settleBalance:F2} sum={sum:F2}");
                Environment.Exit(1);
            }
        }
        Console.WriteLine($"  PASS [{testName}]: All debts correctly settled ✓");
    }

    static Dictionary<string, double> ComputeNetBalances(List<Transaction> transactions)
    {
        var balances = new Dictionary<string, double>();
        for (int i = 0; i < transactions.Count; i++)
        {
            var t = transactions[i];
            if (!balances.ContainsKey(t.From)) balances[t.From] = 0;
            if (!balances.ContainsKey(t.To)) balances[t.To] = 0;
            balances[t.From] -= t.Amount; // giver: outgoing
            balances[t.To] += t.Amount;   // receiver: incoming
        }
        return balances;
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) { Console.WriteLine($"  FAIL: {message}"); Environment.Exit(1); }
        Console.WriteLine($"  PASS: {message} ✓");
    }
}
