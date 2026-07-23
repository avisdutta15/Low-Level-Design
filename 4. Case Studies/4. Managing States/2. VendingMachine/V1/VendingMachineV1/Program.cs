// Vending Machine V1
//
// Design Patterns:
//   - State Pattern: Machine states (Idle, HasMoney, Dispensing) define allowed operations
//   - Chain of Responsibility: Coin change calculation (largest denomination first)
//
// States:
//   IdleState      → accepts money insertion, shows items
//   HasMoneyState  → accepts more money, item selection, cancel
//   DispensingState→ dispenses item, returns change, transitions to Idle
//
// Chain of Responsibility for Change:
//   $10 handler → $5 handler → $1 handler
//   Each handler dispenses as many coins of its denomination as possible, then passes remainder to next

// ─────────────────────────────────────────────
// Item (immutable product definition — no quantity here)
// ─────────────────────────────────────────────
public class Item
{
    public string Code { get; }
    public string Name { get; }
    public int Price { get; }

    public Item(string code, string name, int price)
    {
        Code = code; Name = name; Price = price;
    }

    public override string ToString() => $"[{Code}] {Name} - ${Price}";
}

// ─────────────────────────────────────────────
// Inventory (manages stock separately from item definition)
// ─────────────────────────────────────────────
// Item defines WHAT the product is (code, name, price).
// Inventory tracks HOW MANY are available (stock).
// Separation: same Item can be restocked without recreating the object.
public class Inventory
{
    // stockMap: code → quantity available
    private readonly Dictionary<string, int> _stockMap = new();
    // itemMap: code → Item (product definition)
    private readonly Dictionary<string, Item> _itemMap = new();

    // Add a new item type with initial stock
    public void AddItem(string code, Item item, int quantity)
    {
        _itemMap[code] = item;
        _stockMap[code] = quantity;
    }

    // Reduce stock by 1 after dispensing
    public void ReduceStock(string code)
    {
        if (_stockMap.ContainsKey(code) && _stockMap[code] > 0)
            _stockMap[code]--;
    }

    // Restock: add more quantity to existing item
    public void Restock(string code, int quantity)
    {
        if (_stockMap.ContainsKey(code))
            _stockMap[code] += quantity;
    }

    // Get item definition by code
    public Item? GetItem(string code) => _itemMap.TryGetValue(code, out var item) ? item : null;

    // Check if item is in stock (exists AND quantity > 0)
    public bool IsAvailable(string code) =>
        _stockMap.TryGetValue(code, out var qty) && qty > 0;

    // Get current stock for display
    public int GetStock(string code) => _stockMap.TryGetValue(code, out var qty) ? qty : 0;

    // Get all items for display
    public List<(Item item, int stock)> GetAllItems() =>
        _itemMap.Values.Select(item => (item, GetStock(item.Code))).ToList();
}

// ─────────────────────────────────────────────
// Coin denominations
// ─────────────────────────────────────────────
public enum Coin
{
    Dollar1 = 1,
    Dollar5 = 5,
    Dollar10 = 10
}

// ─────────────────────────────────────────────
// Chain of Responsibility — Coin Change
// ─────────────────────────────────────────────

// Each handler tries to dispense as many coins of its denomination as possible,
// then passes the remaining amount to the next handler in the chain.
// This ensures largest-denomination-first change making (greedy approach).
public abstract class CoinChangeHandler
{
    protected CoinChangeHandler? _next;

    public CoinChangeHandler SetNext(CoinChangeHandler next)
    {
        _next = next;
        return next; // enables chaining: handler1.SetNext(handler2).SetNext(handler3)
    }

    public abstract List<Coin> MakeChange(int amount);
}

public class TenDollarHandler : CoinChangeHandler
{
    public override List<Coin> MakeChange(int amount)
    {
        var coins = new List<Coin>();
        while (amount >= 10) { coins.Add(Coin.Dollar10); amount -= 10; }
        if (amount > 0 && _next != null) coins.AddRange(_next.MakeChange(amount));
        return coins;
    }
}

public class FiveDollarHandler : CoinChangeHandler
{
    public override List<Coin> MakeChange(int amount)
    {
        var coins = new List<Coin>();
        while (amount >= 5) { coins.Add(Coin.Dollar5); amount -= 5; }
        if (amount > 0 && _next != null) coins.AddRange(_next.MakeChange(amount));
        return coins;
    }
}

public class OneDollarHandler : CoinChangeHandler
{
    public override List<Coin> MakeChange(int amount)
    {
        var coins = new List<Coin>();
        while (amount >= 1) { coins.Add(Coin.Dollar1); amount -= 1; }
        return coins; // terminal handler — no next
    }
}

// Factory builds the chain: $10 → $5 → $1
public static class CoinChangeChainFactory
{
    public static CoinChangeHandler CreateChain()
    {
        var ten = new TenDollarHandler();
        var five = new FiveDollarHandler();
        var one = new OneDollarHandler();
        ten.SetNext(five).SetNext(one);
        return ten; // head of chain
    }
}

// ─────────────────────────────────────────────
// State Pattern — Vending Machine States
// ─────────────────────────────────────────────

// IVendingMachineState defines what operations are valid in each state.
// Invalid operations print an error and do nothing (no state transition).
public interface IVendingMachineState
{
    void InsertCoin(VendingMachine machine, Coin coin);
    void SelectItem(VendingMachine machine, string code);
    void Cancel(VendingMachine machine);
    void Dispense(VendingMachine machine);
}

// IdleState: machine is waiting. Only InsertCoin is valid (starts a transaction).
public class IdleState : IVendingMachineState
{
    public void InsertCoin(VendingMachine machine, Coin coin)
    {
        machine.AddMoney((int)coin);
        Console.WriteLine($"    [Machine] Inserted ${(int)coin}. Balance: ${machine.Balance}");
        machine.SetState(new HasMoneyState()); // transition: Idle → HasMoney
    }

    public void SelectItem(VendingMachine machine, string code)
    {
        Console.WriteLine("    [Machine] Please insert money first.");
    }

    public void Cancel(VendingMachine machine)
    {
        Console.WriteLine("    [Machine] No transaction to cancel.");
    }

    public void Dispense(VendingMachine machine)
    {
        Console.WriteLine("    [Machine] Nothing to dispense.");
    }
}

// HasMoneyState: money inserted, waiting for item selection or more money.
public class HasMoneyState : IVendingMachineState
{
    public void InsertCoin(VendingMachine machine, Coin coin)
    {
        machine.AddMoney((int)coin);
        Console.WriteLine($"    [Machine] Inserted ${(int)coin}. Balance: ${machine.Balance}");
    }

    public void SelectItem(VendingMachine machine, string code)
    {
        var item = machine.GetItem(code);
        if (item == null)
        {
            Console.WriteLine($"    [Machine] Item '{code}' not found.");
            return;
        }
        if (!machine.IsItemAvailable(code))
        {
            Console.WriteLine($"    [Machine] {item.Name} is out of stock.");
            return;
        }
        if (machine.Balance < item.Price)
        {
            Console.WriteLine($"    [Machine] Insufficient funds. {item.Name} costs ${item.Price}, balance: ${machine.Balance}");
            return;
        }

        // Selection valid — store selected item and transition to Dispensing
        machine.SelectedItem = item;
        machine.SetState(new DispensingState()); // transition: HasMoney → Dispensing
        machine.Dispense(); // immediately dispense
    }

    public void Cancel(VendingMachine machine)
    {
        // Refund all inserted money
        int refund = machine.Balance;
        var coins = machine.MakeChange(refund);
        machine.ResetBalance();
        Console.WriteLine($"    [Machine] Transaction cancelled. Refund: ${refund} ({FormatCoins(coins)})");
        machine.SetState(new IdleState()); // transition: HasMoney → Idle
    }

    public void Dispense(VendingMachine machine)
    {
        Console.WriteLine("    [Machine] Please select an item first.");
    }

    private string FormatCoins(List<Coin> coins) =>
        string.Join(", ", coins.GroupBy(c => c).Select(g => $"{g.Count()}×${(int)g.Key}"));
}

// DispensingState: item selected, dispensing + returning change.
public class DispensingState : IVendingMachineState
{
    public void InsertCoin(VendingMachine machine, Coin coin)
    {
        Console.WriteLine("    [Machine] Please wait, dispensing in progress.");
    }

    public void SelectItem(VendingMachine machine, string code)
    {
        Console.WriteLine("    [Machine] Please wait, dispensing in progress.");
    }

    public void Cancel(VendingMachine machine)
    {
        Console.WriteLine("    [Machine] Cannot cancel, dispensing in progress.");
    }

    public void Dispense(VendingMachine machine)
    {
        var item = machine.SelectedItem!;

        // Deduct item price from balance
        int change = machine.Balance - item.Price;
        machine.ReduceStock(item.Code); // reduce inventory by 1

        Console.WriteLine($"    [Machine] Dispensing: {item.Name}");

        // Return change using Chain of Responsibility
        if (change > 0)
        {
            var coins = machine.MakeChange(change);
            Console.WriteLine($"    [Machine] Change: ${change} ({FormatCoins(coins)})");
        }
        else
        {
            Console.WriteLine($"    [Machine] No change due.");
        }

        // Reset and transition back to Idle
        machine.ResetBalance();
        machine.SelectedItem = null;
        machine.SetState(new IdleState()); // transition: Dispensing → Idle
    }

    private string FormatCoins(List<Coin> coins) =>
        string.Join(", ", coins.GroupBy(c => c).Select(g => $"{g.Count()}×${(int)g.Key}"));
}

// ─────────────────────────────────────────────
// VendingMachine — context class for State Pattern
// ─────────────────────────────────────────────
public class VendingMachine
{
    private IVendingMachineState _state;
    private readonly Inventory _inventory = new();
    private readonly CoinChangeHandler _changeChain;
    private readonly object _lock = new(); // concurrency: one transaction at a time

    public int Balance { get; private set; }
    public Item? SelectedItem { get; set; }

    public VendingMachine()
    {
        _state = new IdleState();
        _changeChain = CoinChangeChainFactory.CreateChain();
    }

    // ── State management ──
    public void SetState(IVendingMachineState state) => _state = state;

    // ── Money management ──
    public void AddMoney(int amount) => Balance += amount;
    public void ResetBalance() => Balance = 0;

    // ── Change via Chain of Responsibility ──
    public List<Coin> MakeChange(int amount) => _changeChain.MakeChange(amount);

    // ── Inventory delegation ──
    public void AddItem(string code, string name, int price, int quantity)
    {
        _inventory.AddItem(code, new Item(code, name, price), quantity);
    }

    public void Restock(string code, int quantity) => _inventory.Restock(code, quantity);
    public Item? GetItem(string code) => _inventory.GetItem(code);
    public bool IsItemAvailable(string code) => _inventory.IsAvailable(code);
    public void ReduceStock(string code) => _inventory.ReduceStock(code);

    public void ShowItems()
    {
        Console.WriteLine("    ┌──────────────────────────────────────┐");
        Console.WriteLine("    │       VENDING MACHINE ITEMS          │");
        Console.WriteLine("    ├──────────────────────────────────────┤");
        foreach (var (item, stock) in _inventory.GetAllItems())
            Console.WriteLine($"    │  {item} (qty: {stock}){new string(' ', Math.Max(0, 20 - item.Name.Length))}│");
        Console.WriteLine("    └──────────────────────────────────────┘");
    }

    // ── Public operations (delegate to current state, thread-safe) ──

    public void InsertCoin(Coin coin)
    {
        lock (_lock) { _state.InsertCoin(this, coin); }
    }

    public void SelectItem(string code)
    {
        lock (_lock) { _state.SelectItem(this, code); }
    }

    public void Cancel()
    {
        lock (_lock) { _state.Cancel(this); }
    }

    public void Dispense()
    {
        lock (_lock) { _state.Dispense(this); }
    }
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var machine = new VendingMachine();

        // Stock the machine
        machine.AddItem("A1", "Cola", 3, 5);
        machine.AddItem("A2", "Chips", 5, 3);
        machine.AddItem("B1", "Water", 1, 10);
        machine.AddItem("B2", "Candy Bar", 2, 8);
        machine.AddItem("C1", "Energy Drink", 7, 2);

        // ── Show items ──
        Console.WriteLine("=== Available Items ===\n");
        machine.ShowItems();

        // ── Scenario 1: Exact payment ──
        Console.WriteLine("\n=== Scenario 1: Buy Cola ($3 exact) ===\n");
        machine.InsertCoin(Coin.Dollar1);
        machine.InsertCoin(Coin.Dollar1);
        machine.InsertCoin(Coin.Dollar1);
        machine.SelectItem("A1");

        // ── Scenario 2: Overpayment with change ──
        Console.WriteLine("\n=== Scenario 2: Buy Water ($1) with $10 ===\n");
        machine.InsertCoin(Coin.Dollar10);
        machine.SelectItem("B1");

        // ── Scenario 3: Insufficient funds ──
        Console.WriteLine("\n=== Scenario 3: Try Energy Drink ($7) with $5 ===\n");
        machine.InsertCoin(Coin.Dollar5);
        machine.SelectItem("C1"); // should fail — not enough

        // ── Scenario 4: Cancel and refund ──
        Console.WriteLine("\n=== Scenario 4: Cancel transaction ===\n");
        // still have $5 in from scenario 3
        machine.Cancel();

        // ── Scenario 5: Out of stock ──
        Console.WriteLine("\n=== Scenario 5: Out of stock ===\n");
        machine.AddItem("D1", "Limited Edition", 1, 0); // 0 quantity
        machine.InsertCoin(Coin.Dollar1);
        machine.SelectItem("D1");
        machine.Cancel(); // refund the $1

        // ── Scenario 6: Invalid operations ──
        Console.WriteLine("\n=== Scenario 6: Invalid operations ===\n");
        machine.SelectItem("A1");  // no money inserted (Idle state)
        machine.Cancel();           // nothing to cancel (Idle state)

        // ── Scenario 7: Multi-coin purchase with change ──
        Console.WriteLine("\n=== Scenario 7: Buy Chips ($5) with $10+$1 = $11, change $6 ===\n");
        machine.InsertCoin(Coin.Dollar10);
        machine.InsertCoin(Coin.Dollar1);
        machine.SelectItem("A2");

        // ── Final inventory ──
        Console.WriteLine("\n=== Final Inventory ===\n");
        machine.ShowItems();
    }
}
