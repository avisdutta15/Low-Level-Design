using System.Collections.Concurrent;

// Online Food Delivery Service V1
//
// Problem Statement:
//   A digital platform connecting users with nearby restaurants, allowing them to
//   browse menus, place food orders, and have meals delivered by delivery partners.
//
// Core Entities:
//   User               - id, name, email, city, address
//   Restaurant         - id, name, city, address, menu items, isOpen
//   MenuItem           - id, name, price, isAvailable
//   DeliveryAgent      - id, name, city, isAvailable, currentLocation
//   Order              - id, customer, restaurant, items, status, agent, totalAmount
//   OrderStatus        - Pending, Confirmed, Preparing, OutForDelivery, Delivered, Cancelled
//   IOrderObserver     - notified on order status changes
//   DeliveryStrategy   - interface: how to assign a delivery agent (nearest, first-available)
//   OrderService       - facade: place order, update status, cancel, assign agent
//
// Design Patterns:
//   - Observer: IOrderObserver (notify customer, restaurant, agent on status change)
//   - Strategy: IDeliveryStrategy (pluggable agent assignment logic)
//   - Repository: in-memory ConcurrentDictionary for all entities
//   - Facade: OrderService (simple API hiding assignment, validation, notifications)
//
// Order Flow:
//   1. Customer searches restaurants (by city or menu keyword)
//   2. Customer places order (restaurant + items)
//   3. Order status = PENDING → notify restaurant
//   4. Restaurant confirms → CONFIRMED
//   5. Restaurant starts preparing → PREPARING
//   6. DeliveryStrategy assigns agent → OUT_FOR_DELIVERY
//   7. Agent delivers → DELIVERED
//   Cancellation: allowed only while PENDING or CONFIRMED

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────

// OrderStatus represents the lifecycle of an order.
// Transitions: Pending → Confirmed → Preparing → OutForDelivery → Delivered
// Cancel is allowed only from Pending or Confirmed.
public enum OrderStatus
{
    Pending,           // Order placed, restaurant not yet acknowledged
    Confirmed,         // Restaurant accepted the order
    Preparing,         // Restaurant is cooking
    OutForDelivery,    // Agent picked up, on the way to customer
    Delivered,         // Delivered to customer
    Cancelled          // Customer cancelled (only from Pending/Confirmed)
}

// ─────────────────────────────────────────────
// Models
// ─────────────────────────────────────────────

// User represents a customer who can search restaurants and place orders.
public class User
{
    public string Id { get; }
    public string Name { get; }
    public string Email { get; }
    public string City { get; }
    public string Address { get; }

    public User(string id, string name, string email, string city, string address)
    {
        Id = id; Name = name; Email = email; City = city; Address = address;
    }

    public override string ToString() => Name;
}

// MenuItem represents a single dish on a restaurant's menu.
public class MenuItem
{
    public string Id { get; }
    public string Name { get; }
    public double Price { get; }
    public bool IsAvailable { get; set; }

    public MenuItem(string id, string name, double price)
    {
        Id = id; Name = name; Price = price; IsAvailable = true;
    }

    public override string ToString() => $"{Name} (₹{Price})";
}

// Restaurant has a menu, a city location, and can be open/closed.
public class Restaurant
{
    public string Id { get; }
    public string Name { get; }
    public string City { get; }
    public string Address { get; }
    public bool IsOpen { get; set; }
    private readonly List<MenuItem> _menu = new();

    public Restaurant(string id, string name, string city, string address)
    {
        Id = id; Name = name; City = city; Address = address; IsOpen = true;
    }

    public void AddMenuItem(MenuItem item) => _menu.Add(item);
    public List<MenuItem> GetMenu() => _menu.Where(m => m.IsAvailable).ToList();
    public MenuItem? GetMenuItem(string itemId) => _menu.FirstOrDefault(m => m.Id == itemId);

    // Search: does the menu contain an item matching the keyword?
    public bool HasMenuItemMatching(string keyword) =>
        _menu.Any(m => m.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) && m.IsAvailable);

    public override string ToString() => $"{Name} ({City})";
}

// DeliveryAgent delivers orders. Has availability and a city/location.
public class DeliveryAgent
{
    public string Id { get; }
    public string Name { get; }
    public string City { get; }
    public bool IsAvailable { get; set; }
    public double DistanceFromRestaurant { get; set; } // simplified: km from restaurant

    public DeliveryAgent(string id, string name, string city, double distance)
    {
        Id = id; Name = name; City = city; IsAvailable = true; DistanceFromRestaurant = distance;
    }

    public override string ToString() => $"{Name} ({City}, {DistanceFromRestaurant}km)";
}

// Order ties together customer, restaurant, items, agent, and status.
public class Order
{
    public string Id { get; }
    public User Customer { get; }
    public Restaurant Restaurant { get; }
    public List<MenuItem> Items { get; }
    public OrderStatus Status { get; set; }
    public DeliveryAgent? Agent { get; set; }
    public double TotalAmount { get; }
    public DateTime CreatedAt { get; }

    public Order(User customer, Restaurant restaurant, List<MenuItem> items)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Customer = customer;
        Restaurant = restaurant;
        Items = items;
        Status = OrderStatus.Pending;
        TotalAmount = items.Sum(i => i.Price);
        CreatedAt = DateTime.Now;
    }

    public override string ToString() =>
        $"Order({Id}, {Customer.Name}, {Restaurant.Name}, ₹{TotalAmount}, {Status})";
}

// ─────────────────────────────────────────────
// Observer — notified on order status changes
// ─────────────────────────────────────────────

// IOrderObserver is notified whenever an order's status changes.
// Users, Restaurants, and DeliveryAgents all implement this — they each
// receive notifications relevant to their role.
public interface IOrderObserver
{
    void OnOrderStatusChanged(Order order, OrderStatus oldStatus, OrderStatus newStatus);
}

// User as observer — notified about their own orders only (guaranteed by NotificationService)
public class UserOrderObserver : IOrderObserver
{
    private readonly User _user;
    public UserOrderObserver(User user) => _user = user;

    public void OnOrderStatusChanged(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        Console.WriteLine($"    [User:{_user.Name}] Your order {order.Id}: {oldStatus} → {newStatus}");
    }
}

// Restaurant as observer — notified about orders at this restaurant only
public class RestaurantOrderObserver : IOrderObserver
{
    private readonly Restaurant _restaurant;
    public RestaurantOrderObserver(Restaurant restaurant) => _restaurant = restaurant;

    public void OnOrderStatusChanged(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        if (newStatus == OrderStatus.Pending)
            Console.WriteLine($"    [Restaurant:{_restaurant.Name}] New order received! Order {order.Id} (₹{order.TotalAmount})");
        else if (newStatus == OrderStatus.Cancelled)
            Console.WriteLine($"    [Restaurant:{_restaurant.Name}] Order {order.Id} was cancelled by customer");
        else
            Console.WriteLine($"    [Restaurant:{_restaurant.Name}] Order {order.Id}: {oldStatus} → {newStatus}");
    }
}

// DeliveryAgent as observer — notified about orders assigned to them only
public class AgentOrderObserver : IOrderObserver
{
    private readonly DeliveryAgent _agent;
    public AgentOrderObserver(DeliveryAgent agent) => _agent = agent;

    public void OnOrderStatusChanged(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        if (newStatus == OrderStatus.OutForDelivery)
            Console.WriteLine($"    [Agent:{_agent.Name}] Assigned to order {order.Id} — pickup from {order.Restaurant.Name}, deliver to {order.Customer.Name}");
        else if (newStatus == OrderStatus.Delivered)
            Console.WriteLine($"    [Agent:{_agent.Name}] Order {order.Id} delivered! You're now free.");
    }
}

// ─────────────────────────────────────────────
// Delivery Strategy — how to assign an agent
// ─────────────────────────────────────────────

// IDeliveryStrategy decides WHICH agent to assign to an order.
// Different strategies optimize for proximity, load balancing, etc.
public interface IDeliveryStrategy
{
    DeliveryAgent? AssignAgent(List<DeliveryAgent> availableAgents);
}

// Picks the nearest available agent (lowest distance from restaurant).
public class NearestAgentStrategy : IDeliveryStrategy
{
    public DeliveryAgent? AssignAgent(List<DeliveryAgent> availableAgents)
    {
        return availableAgents.OrderBy(a => a.DistanceFromRestaurant).FirstOrDefault();
    }
}

// Picks the first available agent (no preference).
public class FirstAvailableAgentStrategy : IDeliveryStrategy
{
    public DeliveryAgent? AssignAgent(List<DeliveryAgent> availableAgents)
    {
        return availableAgents.FirstOrDefault();
    }
}

// ─────────────────────────────────────────────
// NotificationService — targeted per-order notifications
// ─────────────────────────────────────────────

// Instead of iterating ALL observers and letting each filter itself,
// NotificationService maintains a per-order subscriber map.
// Only the customer, restaurant, and assigned agent for a specific order get notified.
// Lookup is O(1) per order, notification is O(subscribers of this order) — typically 2-3.
public class NotificationService
{
    // Maps userId → their observer instance (for lookup when subscribing to orders)
    private readonly ConcurrentDictionary<string, IOrderObserver> _userObservers = new();
    private readonly ConcurrentDictionary<string, IOrderObserver> _restaurantObservers = new();
    private readonly ConcurrentDictionary<string, IOrderObserver> _agentObservers = new();

    // Per-order subscriber list: orderId → [customer observer, restaurant observer, agent observer]
    private readonly ConcurrentDictionary<string, List<IOrderObserver>> _orderSubscribers = new();

    // Called during RegisterUser — stores the observer for later per-order subscription
    public void RegisterUser(User user)
    {
        _userObservers.TryAdd(user.Id, new UserOrderObserver(user));
    }

    // Called during RegisterRestaurant
    public void RegisterRestaurant(Restaurant restaurant)
    {
        _restaurantObservers.TryAdd(restaurant.Id, new RestaurantOrderObserver(restaurant));
    }

    // Called during RegisterAgent
    public void RegisterAgent(DeliveryAgent agent)
    {
        _agentObservers.TryAdd(agent.Id, new AgentOrderObserver(agent));
    }

    // Called when an order is placed — subscribe the customer + restaurant to this order
    public void SubscribeToOrder(Order order)
    {
        var subscribers = new List<IOrderObserver>();

        if (_userObservers.TryGetValue(order.Customer.Id, out var userObs))
            subscribers.Add(userObs);

        if (_restaurantObservers.TryGetValue(order.Restaurant.Id, out var restObs))
            subscribers.Add(restObs);

        _orderSubscribers.TryAdd(order.Id, subscribers);
    }

    // Called when an agent is assigned — add agent observer to this order's subscribers
    public void SubscribeAgentToOrder(Order order, DeliveryAgent agent)
    {
        if (_orderSubscribers.TryGetValue(order.Id, out var subscribers))
        {
            if (_agentObservers.TryGetValue(agent.Id, out var agentObs))
                subscribers.Add(agentObs);
        }
    }

    // Notify ONLY the subscribers of this specific order (O(1) lookup + O(2-3) iteration)
    public void Notify(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        if (!_orderSubscribers.TryGetValue(order.Id, out var subscribers)) return;

        foreach (var obs in subscribers)
            obs.OnOrderStatusChanged(order, oldStatus, newStatus);
    }
}

// ─────────────────────────────────────────────
// Search Strategy + Factory + Processor
// ─────────────────────────────────────────────

// SearchType enum — what the user wants to search by
public enum SearchType
{
    City,
    Menu,
    Location
}

// ISearchStrategy — each strategy knows how to filter restaurants by one criterion
public interface ISearchStrategy
{
    List<Restaurant> Search(IEnumerable<Restaurant> restaurants, string keyword);
}

// Search by city name
public class CitySearchStrategy : ISearchStrategy
{
    public List<Restaurant> Search(IEnumerable<Restaurant> restaurants, string keyword)
    {
        return restaurants
            .Where(r => r.City.Equals(keyword, StringComparison.OrdinalIgnoreCase) && r.IsOpen)
            .ToList();
    }
}

// Search by menu item keyword (e.g., "Biryani", "Pizza")
public class MenuSearchStrategy : ISearchStrategy
{
    public List<Restaurant> Search(IEnumerable<Restaurant> restaurants, string keyword)
    {
        return restaurants
            .Where(r => r.IsOpen && r.HasMenuItemMatching(keyword))
            .ToList();
    }
}

// Search by location/address (contains match)
public class LocationSearchStrategy : ISearchStrategy
{
    public List<Restaurant> Search(IEnumerable<Restaurant> restaurants, string keyword)
    {
        return restaurants
            .Where(r => r.Address.Contains(keyword, StringComparison.OrdinalIgnoreCase) && r.IsOpen)
            .ToList();
    }
}

// Factory: maps SearchType enum → ISearchStrategy instance
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

// SearchProcessor: OrderService calls this with a SearchType + keyword.
// It resolves the strategy via factory, runs the search, returns results.
// OrderService doesn't know about concrete strategy classes.
public class SearchProcessor
{
    public List<Restaurant> Search(SearchType type, IEnumerable<Restaurant> restaurants, string keyword)
    {
        ISearchStrategy strategy = SearchStrategyFactory.Create(type);
        return strategy.Search(restaurants, keyword);
    }
}

// ─────────────────────────────────────────────
// OrderService — Facade (uses NotificationService + SearchProcessor)
// ─────────────────────────────────────────────

// OrderService orchestrates the order lifecycle.
// It no longer owns observers — delegates to NotificationService.
// It no longer owns search logic — delegates to SearchProcessor.
public class OrderService
{
    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly ConcurrentDictionary<string, Restaurant> _restaurants = new();
    private readonly ConcurrentDictionary<string, DeliveryAgent> _agents = new();
    private readonly ConcurrentDictionary<string, Order> _orders = new();
    private readonly NotificationService _notificationService;
    private readonly SearchProcessor _searchProcessor;
    private readonly IDeliveryStrategy _deliveryStrategy;

    public OrderService(IDeliveryStrategy deliveryStrategy, NotificationService notificationService)
    {
        _deliveryStrategy = deliveryStrategy;
        _notificationService = notificationService;
        _searchProcessor = new SearchProcessor();
    }

    // ── Registration (auto-registers as observer via NotificationService) ──

    public User RegisterUser(string id, string name, string email, string city, string address)
    {
        var user = new User(id, name, email, city, address);
        _users.TryAdd(id, user);
        _notificationService.RegisterUser(user); // auto-subscribe to notifications
        return user;
    }

    public Restaurant RegisterRestaurant(string id, string name, string city, string address)
    {
        var restaurant = new Restaurant(id, name, city, address);
        _restaurants.TryAdd(id, restaurant);
        _notificationService.RegisterRestaurant(restaurant); // auto-subscribe
        return restaurant;
    }

    public DeliveryAgent RegisterAgent(string id, string name, string city, double distance)
    {
        var agent = new DeliveryAgent(id, name, city, distance);
        _agents.TryAdd(id, agent);
        _notificationService.RegisterAgent(agent); // auto-subscribe
        return agent;
    }

    // ── Search (delegates to SearchProcessor) ──

    // Unified search: caller passes SearchType enum + keyword.
    // OrderService doesn't know about strategy classes — SearchProcessor handles it.
    public List<Restaurant> Search(SearchType type, string keyword)
    {
        return _searchProcessor.Search(type, _restaurants.Values, keyword);
    }

    // Convenience methods (thin wrappers over the unified Search)
    public List<Restaurant> SearchByCity(string city) => Search(SearchType.City, city);
    public List<Restaurant> SearchByMenu(string keyword) => Search(SearchType.Menu, keyword);
    public List<Restaurant> SearchByLocation(string location) => Search(SearchType.Location, location);

    // ── Place Order ──

    // Customer places an order with selected items from a restaurant.
    // Validates: user exists, restaurant exists and is open, items exist and are available.
    public Order? PlaceOrder(string userId, string restaurantId, List<string> itemIds)
    {
        if (!_users.TryGetValue(userId, out var user))
        {
            Console.WriteLine($"    [OrderService] User {userId} not found");
            return null;
        }

        if (!_restaurants.TryGetValue(restaurantId, out var restaurant))
        {
            Console.WriteLine($"    [OrderService] Restaurant {restaurantId} not found");
            return null;
        }

        if (!restaurant.IsOpen)
        {
            Console.WriteLine($"    [OrderService] {restaurant.Name} is closed");
            return null;
        }

        // Resolve menu items
        var items = new List<MenuItem>();
        foreach (var itemId in itemIds)
        {
            var item = restaurant.GetMenuItem(itemId);
            if (item == null || !item.IsAvailable)
            {
                Console.WriteLine($"    [OrderService] Item {itemId} not available");
                return null;
            }
            items.Add(item);
        }

        // Create order (status = Pending)
        var order = new Order(user, restaurant, items);
        _orders.TryAdd(order.Id, order);

        // Subscribe the customer + restaurant to this specific order's notifications
        _notificationService.SubscribeToOrder(order);

        Console.WriteLine($"    [OrderService] Order placed: {order}");
        NotifyStatusChange(order, OrderStatus.Pending, OrderStatus.Pending);

        return order;
    }

    // ── Status Updates ──

    // Restaurant confirms the order (Pending → Confirmed)
    public bool ConfirmOrder(string orderId)
    {
        return UpdateStatus(orderId, OrderStatus.Confirmed, 
            new[] { OrderStatus.Pending });
    }

    // Restaurant starts preparing (Confirmed → Preparing)
    public bool StartPreparing(string orderId)
    {
        return UpdateStatus(orderId, OrderStatus.Preparing,
            new[] { OrderStatus.Confirmed });
    }

    // Order ready for pickup — auto-assign delivery agent, move to OutForDelivery
    public bool DispatchOrder(string orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order)) return false;

        if (order.Status != OrderStatus.Preparing)
        {
            Console.WriteLine($"    [OrderService] Cannot dispatch — status is {order.Status}");
            return false;
        }

        // Find available agents in the restaurant's city
        var availableAgents = _agents.Values
            .Where(a => a.IsAvailable && a.City.Equals(order.Restaurant.City, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Use delivery strategy to pick the best agent
        var agent = _deliveryStrategy.AssignAgent(availableAgents);
        if (agent == null)
        {
            Console.WriteLine($"    [OrderService] No delivery agent available in {order.Restaurant.City}");
            return false;
        }

        // Assign agent and mark unavailable
        agent.IsAvailable = false;
        order.Agent = agent;

        // Subscribe the agent to this order's notifications (they'll get delivery updates)
        _notificationService.SubscribeAgentToOrder(order, agent);

        var oldStatus = order.Status;
        order.Status = OrderStatus.OutForDelivery;
        Console.WriteLine($"    [OrderService] Agent {agent.Name} assigned to order {order.Id}");
        NotifyStatusChange(order, oldStatus, order.Status);
        return true;
    }

    // Agent delivers the order (OutForDelivery → Delivered)
    public bool DeliverOrder(string orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order)) return false;

        if (order.Status != OrderStatus.OutForDelivery)
        {
            Console.WriteLine($"    [OrderService] Cannot deliver — status is {order.Status}");
            return false;
        }

        var oldStatus = order.Status;
        order.Status = OrderStatus.Delivered;

        // Release agent
        if (order.Agent != null)
            order.Agent.IsAvailable = true;

        NotifyStatusChange(order, oldStatus, order.Status);
        return true;
    }

    // ── Cancel ──

    // Customer can cancel only if restaurant hasn't started preparing (Pending or Confirmed)
    public bool CancelOrder(string orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order)) return false;

        if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
        {
            Console.WriteLine($"    [OrderService] Cannot cancel — status is {order.Status} (preparation already started)");
            return false;
        }

        var oldStatus = order.Status;
        order.Status = OrderStatus.Cancelled;
        NotifyStatusChange(order, oldStatus, order.Status);
        return true;
    }

    // ── Order History ──

    public List<Order> GetOrderHistory(string userId)
    {
        return _orders.Values.Where(o => o.Customer.Id == userId).OrderByDescending(o => o.CreatedAt).ToList();
    }

    // ── Internal ──

    private bool UpdateStatus(string orderId, OrderStatus newStatus, OrderStatus[] allowedFrom)
    {
        if (!_orders.TryGetValue(orderId, out var order)) return false;

        if (!allowedFrom.Contains(order.Status))
        {
            Console.WriteLine($"    [OrderService] Cannot transition to {newStatus} from {order.Status}");
            return false;
        }

        var oldStatus = order.Status;
        order.Status = newStatus;
        NotifyStatusChange(order, oldStatus, newStatus);
        return true;
    }

    private void NotifyStatusChange(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        _notificationService.Notify(order, oldStatus, newStatus);
    }
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var service = new OrderService(new NearestAgentStrategy(), new NotificationService());

        // ── Register users (auto-subscribed to notifications) ──
        var alice = service.RegisterUser("u1", "Alice", "alice@mail.com", "Mumbai", "Andheri West");
        var bob = service.RegisterUser("u2", "Bob", "bob@mail.com", "Mumbai", "Bandra");

        // ── Register restaurants with menus ──
        var pizzaPlace = service.RegisterRestaurant("r1", "Pizza Palace", "Mumbai", "Juhu");
        pizzaPlace.AddMenuItem(new MenuItem("m1", "Margherita Pizza", 299));
        pizzaPlace.AddMenuItem(new MenuItem("m2", "Pepperoni Pizza", 399));
        pizzaPlace.AddMenuItem(new MenuItem("m3", "Garlic Bread", 149));

        var biryaniHouse = service.RegisterRestaurant("r2", "Biryani House", "Mumbai", "Colaba");
        biryaniHouse.AddMenuItem(new MenuItem("m4", "Chicken Biryani", 249));
        biryaniHouse.AddMenuItem(new MenuItem("m5", "Mutton Biryani", 349));
        biryaniHouse.AddMenuItem(new MenuItem("m6", "Raita", 49));

        var delhiRestaurant = service.RegisterRestaurant("r3", "Delhi Darbar", "Delhi", "CP");
        delhiRestaurant.AddMenuItem(new MenuItem("m7", "Butter Chicken", 299));

        // ── Register delivery agents ──
        var agent1 = service.RegisterAgent("a1", "Ravi", "Mumbai", 2.5);   // 2.5 km from restaurants
        var agent2 = service.RegisterAgent("a2", "Suresh", "Mumbai", 5.0); // 5 km
        var agent3 = service.RegisterAgent("a3", "Amit", "Delhi", 1.0);

        // No manual observer registration needed!
        // RegisterUser/RegisterRestaurant/RegisterAgent automatically subscribe entities
        // to the NotificationService.

        // ── Scenario 1: Search restaurants ──
        Console.WriteLine("=== Scenario 1: Search Restaurants ===\n");

        var mumbaiRestaurants = service.SearchByCity("Mumbai");
        Console.WriteLine($"  Restaurants in Mumbai: {string.Join(", ", mumbaiRestaurants.Select(r => r.Name))}");

        var biryaniResults = service.SearchByMenu("Biryani");
        Console.WriteLine($"  Restaurants with 'Biryani': {string.Join(", ", biryaniResults.Select(r => r.Name))}");

        // ── Scenario 2: Full order lifecycle (happy path) ──
        Console.WriteLine("\n=== Scenario 2: Alice orders Pizza (full lifecycle) ===\n");

        var order1 = service.PlaceOrder("u1", "r1", new List<string> { "m1", "m3" });
        if (order1 != null)
        {
            Console.WriteLine($"    Total: ₹{order1.TotalAmount}");
            service.ConfirmOrder(order1.Id);
            service.StartPreparing(order1.Id);
            service.DispatchOrder(order1.Id);
            service.DeliverOrder(order1.Id);
        }

        // ── Scenario 3: Cancel order (allowed — still Pending) ──
        Console.WriteLine("\n=== Scenario 3: Bob places order then cancels (Pending) ===\n");

        var order2 = service.PlaceOrder("u2", "r2", new List<string> { "m4", "m6" });
        if (order2 != null)
        {
            service.CancelOrder(order2.Id);
        }

        // ── Scenario 4: Cancel after preparation started (should fail) ──
        Console.WriteLine("\n=== Scenario 4: Cancel after Preparing (should FAIL) ===\n");

        var order3 = service.PlaceOrder("u1", "r2", new List<string> { "m5" });
        if (order3 != null)
        {
            service.ConfirmOrder(order3.Id);
            service.StartPreparing(order3.Id);
            // Try to cancel — should fail (already preparing)
            service.CancelOrder(order3.Id);
        }

        // ── Scenario 5: No delivery agent available ──
        Console.WriteLine("\n=== Scenario 5: No agent available ===\n");

        // Make all Mumbai agents unavailable
        agent1.IsAvailable = false;
        agent2.IsAvailable = false;

        var order4 = service.PlaceOrder("u2", "r1", new List<string> { "m2" });
        if (order4 != null)
        {
            service.ConfirmOrder(order4.Id);
            service.StartPreparing(order4.Id);
            service.DispatchOrder(order4.Id); // No agent — should fail
        }

        // ── Scenario 6: Order history ──
        Console.WriteLine("\n=== Scenario 6: Alice's Order History ===\n");
        var history = service.GetOrderHistory("u1");
        foreach (var o in history)
            Console.WriteLine($"    {o}");
    }
}
