using System.Collections.Concurrent;
using System.Collections.Immutable;

// Online Food Delivery Service V2 — Thread-Safe
//
// Same architecture as V1 (NotificationService, SearchProcessor, entity observers)
// with thread-safety fixes:
//   1. Order: per-order lock + TryTransition (atomic status change)
//   2. Agent: pool lock prevents double-assignment
//   3. NotificationService: ImmutableList for per-order subscriber lists
//   4. Restaurant._menu: ImmutableList (safe add during search)
//   5. MenuItem.IsAvailable: volatile

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────
public enum OrderStatus { Pending, Confirmed, Preparing, OutForDelivery, Delivered, Cancelled }
public enum SearchType { City, Menu, Location }

// ─────────────────────────────────────────────
// Models
// ─────────────────────────────────────────────
public class User
{
    public string Id { get; }
    public string Name { get; }
    public string Email { get; }
    public string City { get; }
    public string Address { get; }
    public User(string id, string name, string email, string city, string address)
    { Id = id; Name = name; Email = email; City = city; Address = address; }
    public override string ToString() => Name;
}

public class MenuItem
{
    public string Id { get; }
    public string Name { get; }
    public double Price { get; }
    private volatile bool _isAvailable;
    public bool IsAvailable => _isAvailable;
    public void SetAvailable(bool available) => _isAvailable = available;
    public MenuItem(string id, string name, double price)
    { Id = id; Name = name; Price = price; _isAvailable = true; }
    public override string ToString() => $"{Name} (₹{Price})";
}

public class Restaurant
{
    public string Id { get; }
    public string Name { get; }
    public string City { get; }
    public string Address { get; }
    public bool IsOpen { get; set; }
    private ImmutableList<MenuItem> _menu = ImmutableList<MenuItem>.Empty;

    public Restaurant(string id, string name, string city, string address)
    { Id = id; Name = name; City = city; Address = address; IsOpen = true; }

    public void AddMenuItem(MenuItem item) => ImmutableInterlocked.Update(ref _menu, list => list.Add(item));
    public List<MenuItem> GetMenu() => _menu.Where(m => m.IsAvailable).ToList();
    public MenuItem? GetMenuItem(string itemId) => _menu.FirstOrDefault(m => m.Id == itemId);
    public bool HasMenuItemMatching(string keyword) =>
        _menu.Any(m => m.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) && m.IsAvailable);
    public override string ToString() => $"{Name} ({City})";
}

public class DeliveryAgent
{
    public string Id { get; }
    public string Name { get; }
    public string City { get; }
    public double DistanceFromRestaurant { get; set; }
    private volatile bool _isAvailable;
    public bool IsAvailable => _isAvailable;
    internal void SetAvailable(bool available) => _isAvailable = available;
    public DeliveryAgent(string id, string name, string city, double distance)
    { Id = id; Name = name; City = city; DistanceFromRestaurant = distance; _isAvailable = true; }
    public override string ToString() => $"{Name} ({City}, {DistanceFromRestaurant}km)";
}

// V2: Order with per-order lock + atomic TryTransition
public class Order
{
    public string Id { get; }
    public User Customer { get; }
    public Restaurant Restaurant { get; }
    public List<MenuItem> Items { get; }
    public double TotalAmount { get; }
    public DateTime CreatedAt { get; }

    private readonly object _lock = new();
    private OrderStatus _status;
    private DeliveryAgent? _agent;

    public OrderStatus Status { get { lock (_lock) { return _status; } } }
    public DeliveryAgent? Agent { get { lock (_lock) { return _agent; } } }

    public Order(User customer, Restaurant restaurant, List<MenuItem> items)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Customer = customer; Restaurant = restaurant; Items = items;
        _status = OrderStatus.Pending;
        TotalAmount = items.Sum(i => i.Price);
        CreatedAt = DateTime.Now;
    }

    // Atomic check-and-set — prevents TOCTOU race
    public bool TryTransition(OrderStatus newStatus, params OrderStatus[] allowedFrom)
    {
        lock (_lock)
        {
            if (!allowedFrom.Contains(_status)) return false;
            _status = newStatus;
            return true;
        }
    }

    internal void AssignAgent(DeliveryAgent agent) { lock (_lock) { _agent = agent; } }

    public override string ToString() =>
        $"Order({Id}, {Customer.Name}, {Restaurant.Name}, ₹{TotalAmount}, {Status})";
}

// ─────────────────────────────────────────────
// Observer — entity-based (same as V1)
// ─────────────────────────────────────────────
public interface IOrderObserver
{
    void OnOrderStatusChanged(Order order, OrderStatus oldStatus, OrderStatus newStatus);
}

public class UserOrderObserver : IOrderObserver
{
    private readonly User _user;
    public UserOrderObserver(User user) => _user = user;
    public void OnOrderStatusChanged(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        Console.WriteLine($"    [User:{_user.Name}] Your order {order.Id}: {oldStatus} → {newStatus}");
    }
}

public class RestaurantOrderObserver : IOrderObserver
{
    private readonly Restaurant _restaurant;
    public RestaurantOrderObserver(Restaurant restaurant) => _restaurant = restaurant;
    public void OnOrderStatusChanged(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        if (newStatus == OrderStatus.Pending)
            Console.WriteLine($"    [Restaurant:{_restaurant.Name}] New order received! Order {order.Id} (₹{order.TotalAmount})");
        else if (newStatus == OrderStatus.Cancelled)
            Console.WriteLine($"    [Restaurant:{_restaurant.Name}] Order {order.Id} cancelled");
        else
            Console.WriteLine($"    [Restaurant:{_restaurant.Name}] Order {order.Id}: {oldStatus} → {newStatus}");
    }
}

public class AgentOrderObserver : IOrderObserver
{
    private readonly DeliveryAgent _agent;
    public AgentOrderObserver(DeliveryAgent agent) => _agent = agent;
    public void OnOrderStatusChanged(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        if (newStatus == OrderStatus.OutForDelivery)
            Console.WriteLine($"    [Agent:{_agent.Name}] Assigned to order {order.Id} — deliver to {order.Customer.Name}");
        else if (newStatus == OrderStatus.Delivered)
            Console.WriteLine($"    [Agent:{_agent.Name}] Order {order.Id} delivered! You're free.");
    }
}

// ─────────────────────────────────────────────
// NotificationService — per-order targeted, thread-safe
// ─────────────────────────────────────────────
public class NotificationService
{
    private readonly ConcurrentDictionary<string, IOrderObserver> _userObservers = new();
    private readonly ConcurrentDictionary<string, IOrderObserver> _restaurantObservers = new();
    private readonly ConcurrentDictionary<string, IOrderObserver> _agentObservers = new();

    // V2: per-order subscriber list uses ImmutableList for thread-safe iteration
    private readonly ConcurrentDictionary<string, ImmutableList<IOrderObserver>> _orderSubscribers = new();

    public void RegisterUser(User user) => _userObservers.TryAdd(user.Id, new UserOrderObserver(user));
    public void RegisterRestaurant(Restaurant r) => _restaurantObservers.TryAdd(r.Id, new RestaurantOrderObserver(r));
    public void RegisterAgent(DeliveryAgent a) => _agentObservers.TryAdd(a.Id, new AgentOrderObserver(a));

    // Subscribe customer + restaurant when order is placed
    public void SubscribeToOrder(Order order)
    {
        var subscribers = ImmutableList<IOrderObserver>.Empty;

        if (_userObservers.TryGetValue(order.Customer.Id, out var userObs))
            subscribers = subscribers.Add(userObs);
        if (_restaurantObservers.TryGetValue(order.Restaurant.Id, out var restObs))
            subscribers = subscribers.Add(restObs);

        _orderSubscribers.TryAdd(order.Id, subscribers);
    }

    // Subscribe agent when assigned (thread-safe add to immutable list)
    public void SubscribeAgentToOrder(Order order, DeliveryAgent agent)
    {
        if (_agentObservers.TryGetValue(agent.Id, out var agentObs))
        {
            _orderSubscribers.AddOrUpdate(order.Id,
                ImmutableList<IOrderObserver>.Empty.Add(agentObs),
                (_, existing) => existing.Add(agentObs));
        }
    }

    // Notify only subscribers of THIS order
    public void Notify(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        if (!_orderSubscribers.TryGetValue(order.Id, out var subscribers)) return;
        foreach (var obs in subscribers)
            obs.OnOrderStatusChanged(order, oldStatus, newStatus);
    }
}

// ─────────────────────────────────────────────
// Search Strategy + Factory + Processor (same as V1)
// ─────────────────────────────────────────────
public interface ISearchStrategy
{
    List<Restaurant> Search(IEnumerable<Restaurant> restaurants, string keyword);
}

public class CitySearchStrategy : ISearchStrategy
{
    public List<Restaurant> Search(IEnumerable<Restaurant> restaurants, string keyword) =>
        restaurants.Where(r => r.City.Equals(keyword, StringComparison.OrdinalIgnoreCase) && r.IsOpen).ToList();
}

public class MenuSearchStrategy : ISearchStrategy
{
    public List<Restaurant> Search(IEnumerable<Restaurant> restaurants, string keyword) =>
        restaurants.Where(r => r.IsOpen && r.HasMenuItemMatching(keyword)).ToList();
}

public class LocationSearchStrategy : ISearchStrategy
{
    public List<Restaurant> Search(IEnumerable<Restaurant> restaurants, string keyword) =>
        restaurants.Where(r => r.Address.Contains(keyword, StringComparison.OrdinalIgnoreCase) && r.IsOpen).ToList();
}

public static class SearchStrategyFactory
{
    public static ISearchStrategy Create(SearchType type)
    {
        if (type == SearchType.City) return new CitySearchStrategy();
        else if (type == SearchType.Menu) return new MenuSearchStrategy();
        else if (type == SearchType.Location) return new LocationSearchStrategy();
        else throw new ArgumentException($"Unknown search type: {type}");
    }
}

public class SearchProcessor
{
    public List<Restaurant> Search(SearchType type, IEnumerable<Restaurant> restaurants, string keyword)
    {
        ISearchStrategy strategy = SearchStrategyFactory.Create(type);
        return strategy.Search(restaurants, keyword);
    }
}

// ─────────────────────────────────────────────
// Delivery Strategy
// ─────────────────────────────────────────────
public interface IDeliveryStrategy
{
    DeliveryAgent? AssignAgent(List<DeliveryAgent> availableAgents);
}

public class NearestAgentStrategy : IDeliveryStrategy
{
    public DeliveryAgent? AssignAgent(List<DeliveryAgent> availableAgents) =>
        availableAgents.OrderBy(a => a.DistanceFromRestaurant).FirstOrDefault();
}

// ─────────────────────────────────────────────
// OrderService — thread-safe facade
// ─────────────────────────────────────────────
public class OrderService
{
    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly ConcurrentDictionary<string, Restaurant> _restaurants = new();
    private readonly ConcurrentDictionary<string, DeliveryAgent> _agents = new();
    private readonly ConcurrentDictionary<string, Order> _orders = new();

    private readonly NotificationService _notificationService;
    private readonly SearchProcessor _searchProcessor;
    private readonly IDeliveryStrategy _deliveryStrategy;
    private readonly object _agentPoolLock = new(); // Prevents double-assignment

    public OrderService(IDeliveryStrategy deliveryStrategy, NotificationService notificationService)
    {
        _deliveryStrategy = deliveryStrategy;
        _notificationService = notificationService;
        _searchProcessor = new SearchProcessor();
    }

    // ── Registration (auto-subscribes to NotificationService) ──

    public User RegisterUser(string id, string name, string email, string city, string address)
    {
        var user = new User(id, name, email, city, address);
        _users.TryAdd(id, user);
        _notificationService.RegisterUser(user);
        return user;
    }

    public Restaurant RegisterRestaurant(string id, string name, string city, string address)
    {
        var restaurant = new Restaurant(id, name, city, address);
        _restaurants.TryAdd(id, restaurant);
        _notificationService.RegisterRestaurant(restaurant);
        return restaurant;
    }

    public DeliveryAgent RegisterAgent(string id, string name, string city, double distance)
    {
        var agent = new DeliveryAgent(id, name, city, distance);
        _agents.TryAdd(id, agent);
        _notificationService.RegisterAgent(agent);
        return agent;
    }

    // ── Search (via SearchProcessor) ──

    public List<Restaurant> Search(SearchType type, string keyword) =>
        _searchProcessor.Search(type, _restaurants.Values, keyword);

    public List<Restaurant> SearchByCity(string city) => Search(SearchType.City, city);
    public List<Restaurant> SearchByMenu(string keyword) => Search(SearchType.Menu, keyword);

    // ── Place Order ──

    public Order? PlaceOrder(string userId, string restaurantId, List<string> itemIds)
    {
        if (!_users.TryGetValue(userId, out var user)) return null;
        if (!_restaurants.TryGetValue(restaurantId, out var restaurant)) return null;
        if (!restaurant.IsOpen) return null;

        var items = new List<MenuItem>();
        foreach (var itemId in itemIds)
        {
            var item = restaurant.GetMenuItem(itemId);
            if (item == null || !item.IsAvailable) return null;
            items.Add(item);
        }

        var order = new Order(user, restaurant, items);
        _orders.TryAdd(order.Id, order);

        // Subscribe customer + restaurant to this order's notifications
        _notificationService.SubscribeToOrder(order);

        Console.WriteLine($"    [OrderService] Order placed: {order}");
        _notificationService.Notify(order, OrderStatus.Pending, OrderStatus.Pending);
        return order;
    }

    // ── Status Updates (atomic via Order.TryTransition) ──

    public bool ConfirmOrder(string orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order)) return false;
        if (!order.TryTransition(OrderStatus.Confirmed, OrderStatus.Pending)) return false;
        _notificationService.Notify(order, OrderStatus.Pending, OrderStatus.Confirmed);
        return true;
    }

    public bool StartPreparing(string orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order)) return false;
        if (!order.TryTransition(OrderStatus.Preparing, OrderStatus.Confirmed)) return false;
        _notificationService.Notify(order, OrderStatus.Confirmed, OrderStatus.Preparing);
        return true;
    }

    // Dispatch: agent pool lock prevents double-assignment
    public bool DispatchOrder(string orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order)) return false;

        lock (_agentPoolLock)
        {
            if (order.Status != OrderStatus.Preparing)
            {
                Console.WriteLine($"    [OrderService] Cannot dispatch — status is {order.Status}");
                return false;
            }

            var availableAgents = _agents.Values
                .Where(a => a.IsAvailable && a.City.Equals(order.Restaurant.City, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var agent = _deliveryStrategy.AssignAgent(availableAgents);
            if (agent == null)
            {
                Console.WriteLine($"    [OrderService] No agent available in {order.Restaurant.City}");
                return false;
            }

            agent.SetAvailable(false);
            order.AssignAgent(agent);

            // Subscribe agent to this order's notifications
            _notificationService.SubscribeAgentToOrder(order, agent);

            if (!order.TryTransition(OrderStatus.OutForDelivery, OrderStatus.Preparing))
            {
                agent.SetAvailable(true);
                return false;
            }
        }

        Console.WriteLine($"    [OrderService] Agent {order.Agent?.Name} assigned to order {order.Id}");
        _notificationService.Notify(order, OrderStatus.Preparing, OrderStatus.OutForDelivery);
        return true;
    }

    public bool DeliverOrder(string orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order)) return false;
        if (!order.TryTransition(OrderStatus.Delivered, OrderStatus.OutForDelivery)) return false;

        var agent = order.Agent;
        if (agent != null) agent.SetAvailable(true);

        _notificationService.Notify(order, OrderStatus.OutForDelivery, OrderStatus.Delivered);
        return true;
    }

    public bool CancelOrder(string orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order)) return false;
        if (!order.TryTransition(OrderStatus.Cancelled, OrderStatus.Pending, OrderStatus.Confirmed))
        {
            Console.WriteLine($"    [OrderService] Cannot cancel — preparation already started");
            return false;
        }
        _notificationService.Notify(order, OrderStatus.Pending, OrderStatus.Cancelled);
        return true;
    }

    public List<Order> GetOrderHistory(string userId) =>
        _orders.Values.Where(o => o.Customer.Id == userId).OrderByDescending(o => o.CreatedAt).ToList();
}

// ─────────────────────────────────────────────
// Demo — concurrent operations
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var service = new OrderService(new NearestAgentStrategy(), new NotificationService());

        // Registration auto-subscribes entities to notifications
        var alice = service.RegisterUser("u1", "Alice", "alice@mail.com", "Mumbai", "Andheri");
        var bob = service.RegisterUser("u2", "Bob", "bob@mail.com", "Mumbai", "Bandra");

        var pizza = service.RegisterRestaurant("r1", "Pizza Palace", "Mumbai", "Juhu");
        pizza.AddMenuItem(new MenuItem("m1", "Margherita", 299));
        pizza.AddMenuItem(new MenuItem("m2", "Pepperoni", 399));

        var agent = service.RegisterAgent("a1", "Ravi", "Mumbai", 2.0);

        // ── Scenario 1: Concurrent Confirm + Cancel race ──
        Console.WriteLine("=== Scenario 1: Confirm vs Cancel Race ===\n");

        var order1 = service.PlaceOrder("u1", "r1", new List<string> { "m1" });
        if (order1 != null)
        {
            bool confirmResult = false;
            bool cancelResult = false;

            var confirmTask = Task.Run(() => { confirmResult = service.ConfirmOrder(order1.Id); });
            var cancelTask = Task.Run(() => { cancelResult = service.CancelOrder(order1.Id); });
            Task.WaitAll(confirmTask, cancelTask);

            Console.WriteLine($"\n    Confirm: {(confirmResult ? "SUCCESS" : "FAILED")}");
            Console.WriteLine($"    Cancel:  {(cancelResult ? "SUCCESS" : "FAILED")}");
            Console.WriteLine($"    Final status: {order1.Status}");
            Console.WriteLine($"    (Per-order lock ensures exactly one wins)\n");
        }

        // ── Scenario 2: Two orders race for same agent ──
        Console.WriteLine("=== Scenario 2: Two Orders Race for Same Agent ===\n");

        agent.SetAvailable(true);
        var order2 = service.PlaceOrder("u1", "r1", new List<string> { "m1" });
        var order3 = service.PlaceOrder("u2", "r1", new List<string> { "m2" });

        if (order2 != null && order3 != null)
        {
            service.ConfirmOrder(order2.Id);
            service.StartPreparing(order2.Id);
            service.ConfirmOrder(order3.Id);
            service.StartPreparing(order3.Id);

            bool d2 = false, d3 = false;
            Task.WaitAll(
                Task.Run(() => { d2 = service.DispatchOrder(order2.Id); }),
                Task.Run(() => { d3 = service.DispatchOrder(order3.Id); }));

            Console.WriteLine($"\n    Order2 dispatch: {(d2 ? "SUCCESS" : "FAILED")}");
            Console.WriteLine($"    Order3 dispatch: {(d3 ? "SUCCESS" : "FAILED")}");
            Console.WriteLine($"    (Agent pool lock: only one gets Ravi)\n");
        }

        // ── Scenario 3: Full lifecycle with targeted notifications ──
        Console.WriteLine("=== Scenario 3: Full Lifecycle (targeted notifications) ===\n");
        agent.SetAvailable(true);

        var order4 = service.PlaceOrder("u1", "r1", new List<string> { "m1", "m2" });
        if (order4 != null)
        {
            service.ConfirmOrder(order4.Id);
            service.StartPreparing(order4.Id);
            service.DispatchOrder(order4.Id);
            service.DeliverOrder(order4.Id);
        }
    }
}
