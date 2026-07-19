using System.Collections.Concurrent;

// Car Rental System V1
//
// Problem Statement:
//   A car rental system allows customers to book, use, and return vehicles
//   for a temporary period in exchange for payment.
//
// Core Entities:
//   Vehicle (abstract)    - licensePlate, model, status, pricePerDay, type, bookedUntil
//   Sedan, SUV, etc.      - concrete vehicle types (created via VehicleFactory)
//   VehicleFactory        - Factory pattern: maps VehicleType enum → concrete Vehicle
//   Branch                - a rental location (city) with an inventory of vehicles
//   BranchRepo            - registry of all branches (ConcurrentDictionary)
//   Booking               - reservation record: who, what vehicle, when, equipment, cost
//   BookingRepo           - stores all bookings (ConcurrentDictionary)
//   IBookingStrategy      - Strategy: how to SELECT a vehicle from available pool
//   IPricingStrategy      - Strategy: how to CALCULATE the rental cost
//   Equipment             - add-on items (GPS, ChildSeat, Insurance) with daily rates
//   IPaymentStrategy      - Strategy: how to PROCESS payment (Card, Cash)
//   PaymentProcessor      - takes a payment strategy, calls pay()
//   BookingService        - Facade: orchestrates the entire reservation/pickup/return flow
//   IBookingObserver      - Observer: notified on reservation/pickup/return events
//
// Design Patterns Used:
//   - Strategy: BookingStrategy (vehicle selection), PricingStrategy (cost calc), PaymentStrategy (payment)
//   - Factory: VehicleFactory (create typed vehicles from enum)
//   - Observer: IBookingObserver (decouple event notifications from booking logic)
//   - Repository: BranchRepo, BookingRepo (data access abstraction)
//   - Facade: BookingService (hides orchestration complexity behind simple API)
//
// Booking Flow (3 phases):
//   Phase 1 — CreateReservation:
//     1. Validate pickup/return branches exist
//     2. Acquire global reservation lock (prevent double-booking)
//     3. Find available vehicles of requested type at pickup branch
//     4. BookingStrategy selects best vehicle from available list
//     5. Mark vehicle as Reserved, set bookedUntil date
//     6. Calculate cost via PricingStrategy (vehicle rate + equipment rates × days)
//     7. Create Booking record, store in BookingRepo
//     8. Notify all observers
//
//   Phase 2 — PickupVehicle:
//     1. Look up booking by ID
//     2. Transition vehicle: Reserved → Rented
//     3. Notify observers
//
//   Phase 3 — ReturnVehicle:
//     1. Look up booking
//     2. Calculate late fee if returned after end date (₹500/day penalty)
//     3. Process payment via PaymentProcessor
//     4. Return vehicle to return branch (Rented → Available)
//     5. If one-way rental, remove vehicle from pickup branch
//     6. Notify observers
//
// Vehicle Status Lifecycle:
//   Available → Reserved → Rented → Available
//                  └── (cancel) → Available
//   Available → UnderMaintenance → Available

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────

// VehicleType determines which concrete class is created by the factory
// and which price tier applies when searching for vehicles.
public enum VehicleType
{
    Economy,    // Budget cars (Swift, i20) — lowest daily rate
    Compact,    // Mid-size sedans (City, Civic) — moderate rate
    SUV,        // Sport utility (Creta, Fortuner) — higher rate
    Luxury,     // Premium (Mercedes, BMW) — highest rate
    Van         // Multi-passenger (Innova) — utility rate
}

// VehicleStatus tracks where a vehicle is in its lifecycle.
// Only specific transitions are valid (enforced by business logic, not code in V1).
public enum VehicleStatus
{
    Available,          // Ready to be reserved by a customer
    Reserved,           // Held for a customer, not yet picked up
    Rented,             // Customer has the vehicle, driving it
    UnderMaintenance    // Temporarily unavailable (servicing, repair)
}

// EquipmentType represents add-on items that can be rented alongside the vehicle.
// Each has a daily rate that gets added to the total rental cost.
public enum EquipmentType
{
    GPS,        // Navigation device — ₹100/day
    ChildSeat,  // Child safety seat — ₹150/day
    Insurance   // Damage coverage — ₹200/day
}

// ─────────────────────────────────────────────
// Vehicle (abstract) + Concrete Types + Factory
// ─────────────────────────────────────────────

// Vehicle is the core domain object. It's abstract because different vehicle types
// have different pricing tiers. The concrete classes (Sedan, SUV, etc.) are simple —
// they just pass the correct VehicleType to the base constructor.
// Status is mutable because it changes through the lifecycle (Available → Reserved → Rented → Available).
// BookedUntil tracks when the reservation ends (used for availability checking in future extensions).
public abstract class Vehicle
{
    public string LicensePlate { get; }  // Unique identifier (e.g., "MH01-1001")
    public string Model { get; }         // Display name (e.g., "Swift", "Creta")
    public VehicleStatus Status { get; set; }  // Current lifecycle state
    public double PricePerDay { get; }   // Daily rental rate in ₹
    public VehicleType Type { get; }     // Category for search and pricing
    public DateTime? BookedUntil { get; set; }  // End of current reservation (null if available)

    protected Vehicle(string licensePlate, string model, VehicleType type, double pricePerDay)
    {
        LicensePlate = licensePlate;
        Model = model;
        Type = type;
        PricePerDay = pricePerDay;
        Status = VehicleStatus.Available; // All vehicles start as Available
    }

    public override string ToString() => $"{Type} {Model} ({LicensePlate}) - {Status}";
}

// Concrete vehicle classes — each maps to a VehicleType.
// They're thin wrappers: the only difference is the Type enum value passed to base.
// This allows the factory to create the right type from an enum,
// and future extensions can add type-specific behavior (e.g., fuel policy, mileage limits).
public class Sedan : Vehicle
{
    public Sedan(string licensePlate, string model, double pricePerDay)
        : base(licensePlate, model, VehicleType.Compact, pricePerDay) { }
}

public class SUV : Vehicle
{
    public SUV(string licensePlate, string model, double pricePerDay)
        : base(licensePlate, model, VehicleType.SUV, pricePerDay) { }
}

public class EconomyCar : Vehicle
{
    public EconomyCar(string licensePlate, string model, double pricePerDay)
        : base(licensePlate, model, VehicleType.Economy, pricePerDay) { }
}

public class LuxuryCar : Vehicle
{
    public LuxuryCar(string licensePlate, string model, double pricePerDay)
        : base(licensePlate, model, VehicleType.Luxury, pricePerDay) { }
}

public class Van : Vehicle
{
    public Van(string licensePlate, string model, double pricePerDay)
        : base(licensePlate, model, VehicleType.Van, pricePerDay) { }
}

// VehicleFactory uses the Factory pattern to create vehicles from a VehicleType enum.
// The caller doesn't need to know which concrete class to instantiate —
// just pass the type, and the factory returns the correct Vehicle subclass.
// Adding a new vehicle type: add enum value + add class + add case here.
public static class VehicleFactory
{
    public static Vehicle Create(VehicleType type, string licensePlate, string model, double pricePerDay)
    {
        if (type == VehicleType.Economy) return new EconomyCar(licensePlate, model, pricePerDay);
        else if (type == VehicleType.Compact) return new Sedan(licensePlate, model, pricePerDay);
        else if (type == VehicleType.SUV) return new SUV(licensePlate, model, pricePerDay);
        else if (type == VehicleType.Luxury) return new LuxuryCar(licensePlate, model, pricePerDay);
        else if (type == VehicleType.Van) return new Van(licensePlate, model, pricePerDay);
        else throw new ArgumentException($"Unknown vehicle type: {type}");
    }
}

// ─────────────────────────────────────────────
// Equipment (add-ons with daily rate)
// ─────────────────────────────────────────────

// Equipment represents optional add-on items that the customer can rent alongside the vehicle.
// Each has a DailyRate that gets multiplied by the number of rental days.
// The PricingStrategy includes these in the total cost calculation.
public class Equipment
{
    public EquipmentType Type { get; }
    public double DailyRate { get; }

    public Equipment(EquipmentType type, double dailyRate)
    {
        Type = type;
        DailyRate = dailyRate;
    }
}

// ─────────────────────────────────────────────
// Branch (rental location with vehicle inventory)
// ─────────────────────────────────────────────

// A Branch is a physical rental location (e.g., "Mumbai Airport", "Delhi Central").
// It owns a list of vehicles and provides methods to query availability and return vehicles.
// The lock protects the _vehicles list from concurrent modification —
// e.g., two threads trying to reserve at the same branch simultaneously.
public class Branch
{
    public string Id { get; }    // Unique branch identifier
    public string City { get; }  // City where the branch is located
    private readonly List<Vehicle> _vehicles = new();  // Inventory of vehicles at this branch
    private readonly object _lock = new();  // Protects _vehicles from concurrent access

    public Branch(string id, string city)
    {
        Id = id;
        City = city;
    }

    // Add a vehicle to this branch's inventory (called during setup or after one-way return)
    public void AddVehicle(Vehicle vehicle)
    {
        lock (_lock) { _vehicles.Add(vehicle); }
    }

    // Remove a vehicle from inventory (called when vehicle is transferred to another branch)
    public void RemoveVehicle(Vehicle vehicle)
    {
        lock (_lock) { _vehicles.Remove(vehicle); }
    }

    // Returns a snapshot of available vehicles matching the requested type.
    // "Available" means Status == Available — not Reserved, Rented, or UnderMaintenance.
    // The returned list is a copy — callers can iterate safely.
    public List<Vehicle> GetAvailableByType(VehicleType type)
    {
        lock (_lock)
        {
            return _vehicles.Where(v => v.Type == type && v.Status == VehicleStatus.Available).ToList();
        }
    }

    // Return a vehicle to this branch after a rental.
    // If the vehicle isn't already in this branch's list (one-way rental), add it.
    // Reset status to Available and clear the booking date.
    public void ReturnVehicle(Vehicle vehicle)
    {
        lock (_lock)
        {
            if (!_vehicles.Contains(vehicle))
                _vehicles.Add(vehicle); // One-way rental: vehicle lands at a new branch
            vehicle.Status = VehicleStatus.Available;
            vehicle.BookedUntil = null;
        }
    }

    public override string ToString() => $"Branch({Id}, {City})";
}

// ─────────────────────────────────────────────
// Repositories
// ─────────────────────────────────────────────

// BranchRepo is a simple registry of all branches, keyed by branch ID.
// Uses ConcurrentDictionary for thread-safe registration and lookup.
public class BranchRepo
{
    private readonly ConcurrentDictionary<string, Branch> _branches = new();

    public void Add(Branch branch) => _branches.TryAdd(branch.Id, branch);
    public Branch? Get(string id) => _branches.TryGetValue(id, out var b) ? b : null;
    public List<Branch> GetAll() => _branches.Values.ToList();
}

// Booking is the reservation record — ties together the customer, vehicle, dates, and cost.
// It's created during reservation and updated during return (actualReturnDate, lateFee, payment).
// PlannedDays ensures at least 1 day minimum (even for same-day returns).
public class Booking
{
    public string Id { get; }                     // Unique booking identifier
    public string CustomerId { get; }             // Who made the reservation
    public Vehicle Vehicle { get; }               // The assigned vehicle
    public Branch PickupBranch { get; }           // Where to pick up
    public Branch ReturnBranch { get; }           // Where to return (can differ for one-way)
    public DateTime StartDate { get; }            // Planned pickup date
    public DateTime EndDate { get; }              // Planned return date
    public DateTime? ActualReturnDate { get; set; }  // When actually returned (for late fee calc)
    public List<Equipment> Equipment { get; }     // Add-on items (GPS, insurance, etc.)
    public double TotalCost { get; set; }         // Final cost (vehicle + equipment + late fees)
    public bool IsPaid { get; set; }              // Whether payment was processed successfully

    public Booking(string customerId, Vehicle vehicle, Branch pickupBranch, Branch returnBranch,
        DateTime startDate, DateTime endDate, List<Equipment> equipment)
    {
        Id = Guid.NewGuid().ToString("N")[..8];  // Short unique ID for readability
        CustomerId = customerId;
        Vehicle = vehicle;
        PickupBranch = pickupBranch;
        ReturnBranch = returnBranch;
        StartDate = startDate;
        EndDate = endDate;
        Equipment = equipment;
    }

    // Minimum 1 day rental — even if start and end are same date
    public int PlannedDays => Math.Max(1, (EndDate - StartDate).Days);

    public override string ToString() =>
        $"Booking({Id}, {Vehicle.Type} {Vehicle.Model}, {StartDate:dd-MMM} to {EndDate:dd-MMM}, ₹{TotalCost})";
}

// BookingRepo stores all bookings, keyed by booking ID.
// Supports lookup by ID and by customer (for listing a customer's rental history).
public class BookingRepo
{
    private readonly ConcurrentDictionary<string, Booking> _bookings = new();

    public void Add(Booking booking) => _bookings.TryAdd(booking.Id, booking);
    public Booking? Get(string id) => _bookings.TryGetValue(id, out var b) ? b : null;
    public List<Booking> GetByCustomer(string customerId) =>
        _bookings.Values.Where(b => b.CustomerId == customerId).ToList();
}

// ─────────────────────────────────────────────
// Booking Strategy (how to select a vehicle)
// ─────────────────────────────────────────────

// IBookingStrategy decides WHICH vehicle to assign from the available pool.
// Different strategies optimize for different goals (cost, availability, etc.).
// The strategy receives a pre-filtered list (correct type + Available status)
// and returns the best pick, or null if the list is empty.
public interface IBookingStrategy
{
    Vehicle? FindVehicle(List<Vehicle> available);
}

// CheapestFirstStrategy picks the vehicle with the lowest PricePerDay.
// Good for budget-conscious customers.
public class CheapestFirstStrategy : IBookingStrategy
{
    public Vehicle? FindVehicle(List<Vehicle> available)
    {
        return available.OrderBy(v => v.PricePerDay).FirstOrDefault();
    }
}

// FirstAvailableStrategy picks whatever is first in the list — no preference.
// Good for "I don't care which one, just give me a car" scenarios.
public class FirstAvailableStrategy : IBookingStrategy
{
    public Vehicle? FindVehicle(List<Vehicle> available)
    {
        return available.FirstOrDefault();
    }
}

// ─────────────────────────────────────────────
// Pricing Strategy
// ─────────────────────────────────────────────

// IPricingStrategy decides HOW MUCH the rental costs.
// It takes the vehicle (for daily rate), number of days, equipment list, and start date.
// Start date is needed for strategies that vary by day-of-week (weekend surcharge).
public interface IPricingStrategy
{
    double CalculatePrice(Vehicle vehicle, int days, List<Equipment> equipment, DateTime startDate);
}

// StandardPricing: flat rate.
// Total = (vehiclePricePerDay + sum of equipment daily rates) × number of days.
// No variation by day of week.
public class StandardPricing : IPricingStrategy
{
    public double CalculatePrice(Vehicle vehicle, int days, List<Equipment> equipment, DateTime startDate)
    {
        double vehicleCost = vehicle.PricePerDay * days;
        double equipmentCost = equipment.Sum(e => e.DailyRate) * days;
        return vehicleCost + equipmentCost;
    }
}

// WeekendPricing: 1.5x multiplier on Saturday and Sunday days.
// Iterates day-by-day to apply the correct multiplier per day.
// Monday–Friday: 1.0x, Saturday–Sunday: 1.5x.
public class WeekendPricing : IPricingStrategy
{
    public double CalculatePrice(Vehicle vehicle, int days, List<Equipment> equipment, DateTime startDate)
    {
        double total = 0;
        for (int i = 0; i < days; i++)
        {
            var day = startDate.AddDays(i);
            // 1.5x surcharge on weekends, 1.0x on weekdays
            double multiplier = (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) ? 1.5 : 1.0;
            total += vehicle.PricePerDay * multiplier;
            total += equipment.Sum(e => e.DailyRate) * multiplier;
        }
        return total;
    }
}

// ─────────────────────────────────────────────
// Payment Strategy + Processor
// ─────────────────────────────────────────────

// IPaymentStrategy abstracts the payment method (Card, Cash, UPI, etc.).
// Returns true if payment succeeded, false otherwise.
// In production, these would call external payment gateways.
public interface IPaymentStrategy
{
    bool Pay(Booking booking);
}

// CardPayment simulates charging a credit/debit card.
public class CardPayment : IPaymentStrategy
{
    public bool Pay(Booking booking)
    {
        Console.WriteLine($"    [Card] Charged ₹{booking.TotalCost} for booking {booking.Id}");
        return true; // Simulated success
    }
}

// CashPayment simulates receiving cash at the counter.
public class CashPayment : IPaymentStrategy
{
    public bool Pay(Booking booking)
    {
        Console.WriteLine($"    [Cash] Received ₹{booking.TotalCost} for booking {booking.Id}");
        return true;
    }
}

// PaymentProcessor wraps a strategy and calls Pay().
// The processor is passed into ReturnVehicle — the caller decides the payment method.
public class PaymentProcessor
{
    private readonly IPaymentStrategy _strategy;

    public PaymentProcessor(IPaymentStrategy strategy)
    {
        _strategy = strategy;
    }

    public bool Pay(Booking booking)
    {
        return _strategy.Pay(booking);
    }
}

// ─────────────────────────────────────────────
// Observer
// ─────────────────────────────────────────────

// IBookingObserver is notified at each phase of the booking lifecycle.
// This decouples notification logic (email, SMS, logging) from booking logic.
// Multiple observers can be registered — all are notified on each event.
public interface IBookingObserver
{
    void OnReservationCreated(Booking booking);
    void OnVehiclePickedUp(Booking booking);
    void OnVehicleReturned(Booking booking);
}

// ConsoleBookingObserver prints events to stdout (demo/logging purposes).
// In production, you'd have EmailObserver, SMSObserver, AnalyticsObserver, etc.
public class ConsoleBookingObserver : IBookingObserver
{
    public void OnReservationCreated(Booking booking) =>
        Console.WriteLine($"    [Observer] Reservation created: {booking}");

    public void OnVehiclePickedUp(Booking booking) =>
        Console.WriteLine($"    [Observer] Vehicle picked up: {booking.Vehicle.LicensePlate} by {booking.CustomerId}");

    public void OnVehicleReturned(Booking booking) =>
        Console.WriteLine($"    [Observer] Vehicle returned: {booking.Vehicle.LicensePlate} at {booking.ReturnBranch.City}");
}

// ─────────────────────────────────────────────
// BookingService — Facade
// ─────────────────────────────────────────────

// BookingService is the main entry point (Facade pattern).
// It hides the complexity of vehicle selection, pricing, locking, and payment
// behind three simple methods: CreateReservation, PickupVehicle, ReturnVehicle.
//
// Dependencies (injected via constructor):
//   - BranchRepo: look up branches
//   - BookingRepo: store/retrieve bookings
//   - IBookingStrategy: how to pick a vehicle (pluggable)
//   - IPricingStrategy: how to calculate cost (pluggable)
//
// Thread-safety in V1:
//   - _reservationLock: global lock for CreateReservation (prevents double-booking)
//   - Branch._lock: protects vehicle list operations
//   - V1 gap: PickupVehicle/ReturnVehicle don't lock vehicle status transitions
public class BookingService
{
    private readonly BranchRepo _branchRepo;
    private readonly BookingRepo _bookingRepo;
    private readonly IBookingStrategy _bookingStrategy;
    private readonly IPricingStrategy _pricingStrategy;
    private readonly List<IBookingObserver> _observers = new();

    // Global reservation lock — prevents two threads from reserving the same vehicle.
    // V1 limitation: this serializes ALL reservations across ALL branches.
    // V2 fixes this with per-branch locks.
    private readonly object _reservationLock = new();

    // Late fee: ₹500 per day for every day past the planned return date.
    private const double LateFeePerDay = 500;

    public BookingService(BranchRepo branchRepo, BookingRepo bookingRepo,
        IBookingStrategy bookingStrategy, IPricingStrategy pricingStrategy)
    {
        _branchRepo = branchRepo;
        _bookingRepo = bookingRepo;
        _bookingStrategy = bookingStrategy;
        _pricingStrategy = pricingStrategy;
    }

    // Register an observer to receive booking lifecycle events.
    public void AddObserver(IBookingObserver observer) => _observers.Add(observer);

    // ═══════════════════════════════════════════
    // Phase 1: CreateReservation
    // ═══════════════════════════════════════════
    // Finds an available vehicle, marks it Reserved, creates a Booking.
    // Returns null if no vehicle is available or branches don't exist.
    public Booking? CreateReservation(string customerId, VehicleType vehicleType,
        string pickupBranchId, string returnBranchId, DateTime startDate, DateTime endDate,
        List<Equipment>? equipment = null)
    {
        // Step 1: Validate branches exist
        var pickupBranch = _branchRepo.Get(pickupBranchId);
        var returnBranch = _branchRepo.Get(returnBranchId);
        if (pickupBranch == null || returnBranch == null)
        {
            Console.WriteLine($"    [BookingService] Branch not found");
            return null;
        }

        equipment ??= new List<Equipment>();

        // Step 2: Acquire global lock to prevent double-booking.
        // Without this lock, two threads could both find the same vehicle "Available",
        // and both try to reserve it — resulting in one vehicle assigned to two customers.
        lock (_reservationLock)
        {
            // Step 3: Get all available vehicles of the requested type at the pickup branch.
            // This returns a snapshot — only vehicles with Status == Available.
            var available = pickupBranch.GetAvailableByType(vehicleType);

            // Step 4: Use the booking strategy to select the best vehicle from the available list.
            // CheapestFirstStrategy picks the cheapest; FirstAvailableStrategy picks the first.
            var vehicle = _bookingStrategy.FindVehicle(available);

            if (vehicle == null)
            {
                // No vehicles of the requested type are available at this branch
                Console.WriteLine($"    [BookingService] No {vehicleType} available at {pickupBranch.City}");
                return null;
            }

            // Step 5: Reserve the vehicle — mark it so no other customer can book it.
            // Available → Reserved. The vehicle is now "held" for this customer.
            vehicle.Status = VehicleStatus.Reserved;
            vehicle.BookedUntil = endDate;

            // Step 6: Create the Booking record with all rental details.
            var booking = new Booking(customerId, vehicle, pickupBranch, returnBranch, startDate, endDate, equipment);

            // Step 7: Calculate the total rental cost using the pricing strategy.
            // StandardPricing: (vehicleRate + equipmentRates) × days
            // WeekendPricing: same but 1.5x on Sat/Sun
            int days = booking.PlannedDays;
            booking.TotalCost = _pricingStrategy.CalculatePrice(vehicle, days, equipment, startDate);

            // Step 8: Store the booking in the repository for later retrieval.
            _bookingRepo.Add(booking);

            // Step 9: Notify all registered observers that a reservation was created.
            foreach (var obs in _observers) obs.OnReservationCreated(booking);

            return booking;
        }
        // Lock released — other threads can now make reservations.
    }

    // ═══════════════════════════════════════════
    // Phase 2: PickupVehicle
    // ═══════════════════════════════════════════
    // Customer arrives at the branch and picks up the reserved vehicle.
    // Transitions the vehicle from Reserved → Rented.
    public Booking? PickupVehicle(string bookingId)
    {
        // Look up the booking
        var booking = _bookingRepo.Get(bookingId);
        if (booking == null)
        {
            Console.WriteLine($"    [BookingService] Booking {bookingId} not found");
            return null;
        }

        // Transition: Reserved → Rented (customer now has the vehicle)
        // V1 gap: this status change happens without any lock — race condition possible.
        booking.Vehicle.Status = VehicleStatus.Rented;

        // Notify observers
        foreach (var obs in _observers) obs.OnVehiclePickedUp(booking);

        return booking;
    }

    // ═══════════════════════════════════════════
    // Phase 3: ReturnVehicle
    // ═══════════════════════════════════════════
    // Customer returns the vehicle. Calculates final cost (with late fees),
    // processes payment, and puts the vehicle back in the return branch's inventory.
    public Booking? ReturnVehicle(string bookingId, DateTime actualReturnDate, PaymentProcessor paymentProcessor)
    {
        // Look up the booking
        var booking = _bookingRepo.Get(bookingId);
        if (booking == null)
        {
            Console.WriteLine($"    [BookingService] Booking {bookingId} not found");
            return null;
        }

        booking.ActualReturnDate = actualReturnDate;

        // Check for late return: if actualReturnDate > planned EndDate, apply penalty.
        // Late fee = ₹500 per extra day.
        int lateDays = Math.Max(0, (actualReturnDate - booking.EndDate).Days);
        if (lateDays > 0)
        {
            double lateFee = lateDays * LateFeePerDay;
            booking.TotalCost += lateFee;
            Console.WriteLine($"    [BookingService] Late return: {lateDays} extra day(s), +₹{lateFee} fee");
        }

        // Process payment via the provided PaymentProcessor.
        // The caller decides the payment method (Card, Cash) by passing the right processor.
        bool paid = paymentProcessor.Pay(booking);
        booking.IsPaid = paid;

        // Return vehicle to the return branch.
        // Branch.ReturnVehicle handles: add to inventory if new branch, set status Available.
        booking.ReturnBranch.ReturnVehicle(booking.Vehicle);

        // If this is a one-way rental (pickup ≠ return), remove vehicle from pickup branch.
        // The vehicle has physically moved to a different city.
        if (booking.PickupBranch.Id != booking.ReturnBranch.Id)
        {
            booking.PickupBranch.RemoveVehicle(booking.Vehicle);
        }

        // Notify observers
        foreach (var obs in _observers) obs.OnVehicleReturned(booking);

        return booking;
    }
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        // ── Setup: Create repositories ──
        var branchRepo = new BranchRepo();
        var bookingRepo = new BookingRepo();

        // ── Setup: Create branches (rental locations) ──
        var mumbai = new Branch("br1", "Mumbai");
        var delhi = new Branch("br2", "Delhi");
        branchRepo.Add(mumbai);
        branchRepo.Add(delhi);

        // ── Setup: Add vehicles to branches using the factory ──
        // Mumbai has: 2 Economy, 1 SUV, 1 Luxury
        mumbai.AddVehicle(VehicleFactory.Create(VehicleType.Economy, "MH01-1001", "Swift", 800));
        mumbai.AddVehicle(VehicleFactory.Create(VehicleType.Economy, "MH01-1002", "i20", 900));
        mumbai.AddVehicle(VehicleFactory.Create(VehicleType.SUV, "MH01-2001", "Creta", 1500));
        mumbai.AddVehicle(VehicleFactory.Create(VehicleType.Luxury, "MH01-3001", "Mercedes C-Class", 5000));
        // Delhi has: 1 Compact, 1 SUV
        delhi.AddVehicle(VehicleFactory.Create(VehicleType.Compact, "DL01-1001", "City", 1200));
        delhi.AddVehicle(VehicleFactory.Create(VehicleType.SUV, "DL01-2001", "Fortuner", 2500));

        // ── Setup: Create BookingService with strategies ──
        // CheapestFirstStrategy: always picks the cheapest available vehicle
        // StandardPricing: flat rate per day (no weekend surcharge)
        var service = new BookingService(branchRepo, bookingRepo,
            new CheapestFirstStrategy(), new StandardPricing());

        // Register an observer to log all booking events to console
        service.AddObserver(new ConsoleBookingObserver());

        // ── Define equipment options ──
        var gps = new Equipment(EquipmentType.GPS, 100);          // ₹100/day
        var childSeat = new Equipment(EquipmentType.ChildSeat, 150); // ₹150/day
        var insurance = new Equipment(EquipmentType.Insurance, 200); // ₹200/day

        // ══════════════════════════════════════════════════════
        // Scenario 1: Simple reservation + pickup + on-time return
        // Alice rents the cheapest Economy car with GPS for 3 days
        // ══════════════════════════════════════════════════════
        Console.WriteLine("=== Scenario 1: Alice rents Economy in Mumbai (3 days, with GPS) ===\n");
        var booking1 = service.CreateReservation("alice", VehicleType.Economy,
            "br1", "br1",  // same branch pickup and return
            new DateTime(2025, 7, 21), new DateTime(2025, 7, 24),
            new List<Equipment> { gps });

        if (booking1 != null)
        {
            // Cost breakdown: 3 days × (₹800 vehicle + ₹100 GPS) = 3 × ₹900 = ₹2700
            Console.WriteLine($"    Cost: ₹{booking1.TotalCost} ({booking1.PlannedDays} days × (₹{booking1.Vehicle.PricePerDay} + ₹100 GPS))");
            service.PickupVehicle(booking1.Id);
            // On-time return — no late fee
            service.ReturnVehicle(booking1.Id, new DateTime(2025, 7, 24), new PaymentProcessor(new CardPayment()));
        }

        // ══════════════════════════════════════════════════════
        // Scenario 2: One-way rental (Mumbai → Delhi)
        // Bob picks up SUV in Mumbai, returns it in Delhi
        // ══════════════════════════════════════════════════════
        Console.WriteLine("\n=== Scenario 2: Bob rents SUV Mumbai→Delhi (2 days, with Insurance) ===\n");
        var booking2 = service.CreateReservation("bob", VehicleType.SUV,
            "br1", "br2",  // pickup Mumbai, return Delhi (one-way!)
            new DateTime(2025, 7, 22), new DateTime(2025, 7, 24),
            new List<Equipment> { insurance });

        if (booking2 != null)
        {
            // Cost: 2 days × (₹1500 + ₹200 insurance) = 2 × ₹1700 = ₹3400
            Console.WriteLine($"    Cost: ₹{booking2.TotalCost}");
            service.PickupVehicle(booking2.Id);
            // Return to Delhi — vehicle moves from Mumbai inventory to Delhi inventory
            service.ReturnVehicle(booking2.Id, new DateTime(2025, 7, 24), new PaymentProcessor(new CashPayment()));
        }

        // ══════════════════════════════════════════════════════
        // Scenario 3: Late return with penalty
        // Charlie books Jul 25–27 but returns on Jul 29 (2 days late)
        // ══════════════════════════════════════════════════════
        Console.WriteLine("\n=== Scenario 3: Charlie rents Economy, returns 2 days late ===\n");
        var booking3 = service.CreateReservation("charlie", VehicleType.Economy,
            "br1", "br1",
            new DateTime(2025, 7, 25), new DateTime(2025, 7, 27));

        if (booking3 != null)
        {
            // Planned: 2 days × ₹800 = ₹1600
            Console.WriteLine($"    Planned cost: ₹{booking3.TotalCost}");
            service.PickupVehicle(booking3.Id);
            // Returns 2 days late → +₹1000 penalty (2 × ₹500)
            service.ReturnVehicle(booking3.Id, new DateTime(2025, 7, 29), new PaymentProcessor(new CardPayment()));
            // Final: ₹1600 + ₹1000 = ₹2600
            Console.WriteLine($"    Final cost (with late fee): ₹{booking3.TotalCost}");
        }

        // ══════════════════════════════════════════════════════
        // Scenario 4: No vehicle available
        // Dave tries to rent Luxury in Delhi — but Delhi has no Luxury cars
        // ══════════════════════════════════════════════════════
        Console.WriteLine("\n=== Scenario 4: Dave tries to rent Luxury in Delhi (none available) ===\n");
        var booking4 = service.CreateReservation("dave", VehicleType.Luxury,
            "br2", "br2",
            new DateTime(2025, 7, 21), new DateTime(2025, 7, 23));
        // Output: "No Luxury available at Delhi"
    }
}
