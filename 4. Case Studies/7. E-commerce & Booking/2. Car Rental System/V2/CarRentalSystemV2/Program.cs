using System.Collections.Concurrent;
using System.Collections.Immutable;

// Car Rental System V2 — Fully Thread-Safe
//
// V1 Thread-Safety Gaps:
//   1. Vehicle.Status — public setter, unprotected across threads
//   2. _observers (List) — AddObserver during notification crashes
//   3. GetAvailableByType returns snapshot but status can change before reserve (TOCTOU)
//   4. PickupVehicle/ReturnVehicle — no lock on vehicle state transitions
//   5. Global _reservationLock — serializes ALL branches (poor throughput)
//
// V2 Fixes:
//   1. Vehicle.Status → volatile + internal SetStatus(), only changed through Branch under lock
//   2. Observers → ImmutableList with ImmutableInterlocked (snapshot-safe iteration)
//   3. Branch.ReserveVehicle() — find + reserve in ONE lock (atomic, no TOCTOU)
//   4. All vehicle state transitions go through Branch (per-branch lock)
//   5. Per-branch locks instead of one global lock — different branches run in parallel

// ─────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────
public enum VehicleType { Economy, Compact, SUV, Luxury, Van }
public enum VehicleStatus { Available, Reserved, Rented, UnderMaintenance }
public enum EquipmentType { GPS, ChildSeat, Insurance }

// ─────────────────────────────────────────────
// Vehicle — volatile status, internal setter
// ─────────────────────────────────────────────
public abstract class Vehicle
{
    public string LicensePlate { get; }
    public string Model { get; }
    public VehicleType Type { get; }
    public double PricePerDay { get; }
    public DateTime? BookedUntil { get; set; }

    // V2: volatile for cross-thread visibility, internal setter for controlled mutation
    private volatile VehicleStatus _status;
    public VehicleStatus Status => _status;
    internal void SetStatus(VehicleStatus status) => _status = status;

    protected Vehicle(string licensePlate, string model, VehicleType type, double pricePerDay)
    {
        LicensePlate = licensePlate;
        Model = model;
        Type = type;
        PricePerDay = pricePerDay;
        _status = VehicleStatus.Available;
    }

    public override string ToString() => $"{Type} {Model} ({LicensePlate}) - {Status}";
}

public class Sedan : Vehicle
{
    public Sedan(string lp, string model, double ppd) : base(lp, model, VehicleType.Compact, ppd) { }
}
public class SUV : Vehicle
{
    public SUV(string lp, string model, double ppd) : base(lp, model, VehicleType.SUV, ppd) { }
}
public class EconomyCar : Vehicle
{
    public EconomyCar(string lp, string model, double ppd) : base(lp, model, VehicleType.Economy, ppd) { }
}
public class LuxuryCar : Vehicle
{
    public LuxuryCar(string lp, string model, double ppd) : base(lp, model, VehicleType.Luxury, ppd) { }
}
public class Van : Vehicle
{
    public Van(string lp, string model, double ppd) : base(lp, model, VehicleType.Van, ppd) { }
}

public static class VehicleFactory
{
    public static Vehicle Create(VehicleType type, string lp, string model, double ppd)
    {
        if (type == VehicleType.Economy) return new EconomyCar(lp, model, ppd);
        else if (type == VehicleType.Compact) return new Sedan(lp, model, ppd);
        else if (type == VehicleType.SUV) return new SUV(lp, model, ppd);
        else if (type == VehicleType.Luxury) return new LuxuryCar(lp, model, ppd);
        else if (type == VehicleType.Van) return new Van(lp, model, ppd);
        else throw new ArgumentException($"Unknown type: {type}");
    }
}

// ─────────────────────────────────────────────
// Equipment
// ─────────────────────────────────────────────
public class Equipment
{
    public EquipmentType Type { get; }
    public double DailyRate { get; }
    public Equipment(EquipmentType type, double dailyRate) { Type = type; DailyRate = dailyRate; }
}

// ─────────────────────────────────────────────
// Branch — per-branch lock, atomic find+reserve
// ─────────────────────────────────────────────
public class Branch
{
    public string Id { get; }
    public string City { get; }
    private readonly List<Vehicle> _vehicles = new();
    private readonly object _lock = new(); // Per-branch lock

    public Branch(string id, string city) { Id = id; City = city; }

    public void AddVehicle(Vehicle vehicle)
    {
        lock (_lock) { _vehicles.Add(vehicle); }
    }

    public void RemoveVehicle(Vehicle vehicle)
    {
        lock (_lock) { _vehicles.Remove(vehicle); }
    }

    // V2: Atomic find + reserve in ONE lock acquisition.
    // Eliminates the TOCTOU gap where V1 returned a list then reserved separately.
    // The bookingStrategy runs INSIDE the lock — finds and reserves atomically.
    public Vehicle? ReserveVehicle(VehicleType type, IBookingStrategy strategy, DateTime bookedUntil)
    {
        lock (_lock)
        {
            var available = _vehicles.Where(v => v.Type == type && v.Status == VehicleStatus.Available).ToList();
            var vehicle = strategy.FindVehicle(available);

            if (vehicle == null) return null;

            // Reserve atomically — no gap between find and status change
            vehicle.SetStatus(VehicleStatus.Reserved);
            vehicle.BookedUntil = bookedUntil;
            return vehicle;
        }
    }

    // V2: Pickup transitions Reserved → Rented under lock
    public bool PickupVehicle(Vehicle vehicle)
    {
        lock (_lock)
        {
            if (vehicle.Status != VehicleStatus.Reserved) return false;
            vehicle.SetStatus(VehicleStatus.Rented);
            return true;
        }
    }

    // V2: Return transitions Rented → Available under lock, adds to branch if one-way
    public void ReturnVehicle(Vehicle vehicle)
    {
        lock (_lock)
        {
            if (!_vehicles.Contains(vehicle))
                _vehicles.Add(vehicle); // one-way: vehicle lands at new branch
            vehicle.SetStatus(VehicleStatus.Available);
            vehicle.BookedUntil = null;
        }
    }

    public override string ToString() => $"Branch({Id}, {City})";
}

// ─────────────────────────────────────────────
// Repositories
// ─────────────────────────────────────────────
public class BranchRepo
{
    private readonly ConcurrentDictionary<string, Branch> _branches = new();
    public void Add(Branch branch) => _branches.TryAdd(branch.Id, branch);
    public Branch? Get(string id) => _branches.TryGetValue(id, out var b) ? b : null;
}

public class Booking
{
    public string Id { get; }
    public string CustomerId { get; }
    public Vehicle Vehicle { get; }
    public Branch PickupBranch { get; }
    public Branch ReturnBranch { get; }
    public DateTime StartDate { get; }
    public DateTime EndDate { get; }
    public DateTime? ActualReturnDate { get; set; }
    public List<Equipment> Equipment { get; }
    public double TotalCost { get; set; }
    public bool IsPaid { get; set; }

    public Booking(string customerId, Vehicle vehicle, Branch pickupBranch, Branch returnBranch,
        DateTime startDate, DateTime endDate, List<Equipment> equipment)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        CustomerId = customerId;
        Vehicle = vehicle;
        PickupBranch = pickupBranch;
        ReturnBranch = returnBranch;
        StartDate = startDate;
        EndDate = endDate;
        Equipment = equipment;
    }

    public int PlannedDays => Math.Max(1, (EndDate - StartDate).Days);
    public override string ToString() =>
        $"Booking({Id}, {Vehicle.Type} {Vehicle.Model}, {StartDate:dd-MMM} to {EndDate:dd-MMM}, ₹{TotalCost})";
}

public class BookingRepo
{
    private readonly ConcurrentDictionary<string, Booking> _bookings = new();
    public void Add(Booking booking) => _bookings.TryAdd(booking.Id, booking);
    public Booking? Get(string id) => _bookings.TryGetValue(id, out var b) ? b : null;
}

// ─────────────────────────────────────────────
// Booking Strategy
// ─────────────────────────────────────────────
public interface IBookingStrategy
{
    Vehicle? FindVehicle(List<Vehicle> available);
}

public class CheapestFirstStrategy : IBookingStrategy
{
    public Vehicle? FindVehicle(List<Vehicle> available) => available.OrderBy(v => v.PricePerDay).FirstOrDefault();
}

public class FirstAvailableStrategy : IBookingStrategy
{
    public Vehicle? FindVehicle(List<Vehicle> available) => available.FirstOrDefault();
}

// ─────────────────────────────────────────────
// Pricing Strategy
// ─────────────────────────────────────────────
public interface IPricingStrategy
{
    double CalculatePrice(Vehicle vehicle, int days, List<Equipment> equipment, DateTime startDate);
}

public class StandardPricing : IPricingStrategy
{
    public double CalculatePrice(Vehicle vehicle, int days, List<Equipment> equipment, DateTime startDate)
    {
        return (vehicle.PricePerDay + equipment.Sum(e => e.DailyRate)) * days;
    }
}

public class WeekendPricing : IPricingStrategy
{
    public double CalculatePrice(Vehicle vehicle, int days, List<Equipment> equipment, DateTime startDate)
    {
        double total = 0;
        for (int i = 0; i < days; i++)
        {
            var day = startDate.AddDays(i);
            double mult = (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) ? 1.5 : 1.0;
            total += (vehicle.PricePerDay + equipment.Sum(e => e.DailyRate)) * mult;
        }
        return total;
    }
}

// ─────────────────────────────────────────────
// Payment
// ─────────────────────────────────────────────
public interface IPaymentStrategy
{
    bool Pay(Booking booking);
}

public class CardPayment : IPaymentStrategy
{
    public bool Pay(Booking booking)
    {
        Console.WriteLine($"    [Card] Charged ₹{booking.TotalCost} for booking {booking.Id}");
        return true;
    }
}

public class CashPayment : IPaymentStrategy
{
    public bool Pay(Booking booking)
    {
        Console.WriteLine($"    [Cash] Received ₹{booking.TotalCost} for booking {booking.Id}");
        return true;
    }
}

public class PaymentProcessor
{
    private readonly IPaymentStrategy _strategy;
    public PaymentProcessor(IPaymentStrategy strategy) => _strategy = strategy;
    public bool Pay(Booking booking) => _strategy.Pay(booking);
}

// ─────────────────────────────────────────────
// Observer — ImmutableList for thread-safety
// ─────────────────────────────────────────────
public interface IBookingObserver
{
    void OnReservationCreated(Booking booking);
    void OnVehiclePickedUp(Booking booking);
    void OnVehicleReturned(Booking booking);
}

public class ConsoleBookingObserver : IBookingObserver
{
    public void OnReservationCreated(Booking booking) =>
        Console.WriteLine($"    [Observer] Reserved: {booking}");
    public void OnVehiclePickedUp(Booking booking) =>
        Console.WriteLine($"    [Observer] Picked up: {booking.Vehicle.LicensePlate} by {booking.CustomerId}");
    public void OnVehicleReturned(Booking booking) =>
        Console.WriteLine($"    [Observer] Returned: {booking.Vehicle.LicensePlate} at {booking.ReturnBranch.City}");
}

// ─────────────────────────────────────────────
// BookingService — per-branch locks, no global reservation lock
// ─────────────────────────────────────────────
public class BookingService
{
    private readonly BranchRepo _branchRepo;
    private readonly BookingRepo _bookingRepo;
    private readonly IBookingStrategy _bookingStrategy;
    private readonly IPricingStrategy _pricingStrategy;
    // V2: ImmutableList for thread-safe observer management
    private ImmutableList<IBookingObserver> _observers = ImmutableList<IBookingObserver>.Empty;

    private const double LateFeePerDay = 500;

    public BookingService(BranchRepo branchRepo, BookingRepo bookingRepo,
        IBookingStrategy bookingStrategy, IPricingStrategy pricingStrategy)
    {
        _branchRepo = branchRepo;
        _bookingRepo = bookingRepo;
        _bookingStrategy = bookingStrategy;
        _pricingStrategy = pricingStrategy;
    }

    // V2: ImmutableInterlocked — safe to add observers while notifications are in-flight
    public void AddObserver(IBookingObserver observer)
    {
        ImmutableInterlocked.Update(ref _observers, list => list.Add(observer));
    }

    private void NotifyReservation(Booking b) { foreach (var o in _observers) o.OnReservationCreated(b); }
    private void NotifyPickup(Booking b) { foreach (var o in _observers) o.OnVehiclePickedUp(b); }
    private void NotifyReturn(Booking b) { foreach (var o in _observers) o.OnVehicleReturned(b); }

    // V2: No global _reservationLock. The per-branch lock inside Branch.ReserveVehicle()
    // handles concurrency. Two reservations at DIFFERENT branches run in parallel.
    public Booking? CreateReservation(string customerId, VehicleType vehicleType,
        string pickupBranchId, string returnBranchId, DateTime startDate, DateTime endDate,
        List<Equipment>? equipment = null)
    {
        var pickupBranch = _branchRepo.Get(pickupBranchId);
        var returnBranch = _branchRepo.Get(returnBranchId);
        if (pickupBranch == null || returnBranch == null)
        {
            Console.WriteLine($"    [BookingService] Branch not found");
            return null;
        }

        equipment ??= new List<Equipment>();

        // V2: Atomic find+reserve inside Branch (per-branch lock)
        var vehicle = pickupBranch.ReserveVehicle(vehicleType, _bookingStrategy, endDate);
        if (vehicle == null)
        {
            Console.WriteLine($"    [BookingService] No {vehicleType} available at {pickupBranch.City}");
            return null;
        }

        var booking = new Booking(customerId, vehicle, pickupBranch, returnBranch, startDate, endDate, equipment);
        booking.TotalCost = _pricingStrategy.CalculatePrice(vehicle, booking.PlannedDays, equipment, startDate);
        _bookingRepo.Add(booking);

        NotifyReservation(booking);
        return booking;
    }

    // V2: Pickup goes through Branch (per-branch lock protects status transition)
    public Booking? PickupVehicle(string bookingId)
    {
        var booking = _bookingRepo.Get(bookingId);
        if (booking == null) return null;

        bool success = booking.PickupBranch.PickupVehicle(booking.Vehicle);
        if (!success)
        {
            Console.WriteLine($"    [BookingService] Cannot pickup — vehicle not in Reserved state");
            return null;
        }

        NotifyPickup(booking);
        return booking;
    }

    // V2: Return goes through Branch (per-branch lock protects status transition)
    public Booking? ReturnVehicle(string bookingId, DateTime actualReturnDate, PaymentProcessor processor)
    {
        var booking = _bookingRepo.Get(bookingId);
        if (booking == null) return null;

        booking.ActualReturnDate = actualReturnDate;

        // Late fee
        int lateDays = Math.Max(0, (actualReturnDate - booking.EndDate).Days);
        if (lateDays > 0)
        {
            double lateFee = lateDays * LateFeePerDay;
            booking.TotalCost += lateFee;
            Console.WriteLine($"    [BookingService] Late: {lateDays} day(s), +₹{lateFee}");
        }

        // Payment
        booking.IsPaid = processor.Pay(booking);

        // Return vehicle to branch (per-branch lock inside)
        booking.ReturnBranch.ReturnVehicle(booking.Vehicle);

        // Remove from pickup branch if one-way
        if (booking.PickupBranch.Id != booking.ReturnBranch.Id)
            booking.PickupBranch.RemoveVehicle(booking.Vehicle);

        NotifyReturn(booking);
        return booking;
    }
}

// ─────────────────────────────────────────────
// Demo — concurrent reservations
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var branchRepo = new BranchRepo();
        var bookingRepo = new BookingRepo();

        var mumbai = new Branch("br1", "Mumbai");
        var delhi = new Branch("br2", "Delhi");
        branchRepo.Add(mumbai);
        branchRepo.Add(delhi);

        // Only ONE economy car in Mumbai — race condition target
        mumbai.AddVehicle(VehicleFactory.Create(VehicleType.Economy, "MH01-1001", "Swift", 800));
        mumbai.AddVehicle(VehicleFactory.Create(VehicleType.SUV, "MH01-2001", "Creta", 1500));
        delhi.AddVehicle(VehicleFactory.Create(VehicleType.Compact, "DL01-1001", "City", 1200));

        var service = new BookingService(branchRepo, bookingRepo,
            new CheapestFirstStrategy(), new StandardPricing());
        service.AddObserver(new ConsoleBookingObserver());

        // ── Concurrent race: Alice and Bob both want the only Economy in Mumbai ──
        Console.WriteLine("=== Concurrent Race: Alice vs Bob for same Economy car ===\n");

        Booking? aliceBooking = null;
        Booking? bobBooking = null;

        var aliceTask = Task.Run(() =>
        {
            aliceBooking = service.CreateReservation("alice", VehicleType.Economy,
                "br1", "br1", new DateTime(2025, 7, 21), new DateTime(2025, 7, 24));
        });

        var bobTask = Task.Run(() =>
        {
            bobBooking = service.CreateReservation("bob", VehicleType.Economy,
                "br1", "br1", new DateTime(2025, 7, 21), new DateTime(2025, 7, 24));
        });

        Task.WaitAll(aliceTask, bobTask);

        Console.WriteLine($"\n  Alice: {(aliceBooking != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"  Bob:   {(bobBooking != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"  Exactly one should succeed (no double-booking).\n");

        // ── Concurrent reservations at DIFFERENT branches (both succeed, parallel) ──
        Console.WriteLine("=== Concurrent: Charlie (Mumbai SUV) + Dave (Delhi Compact) ===\n");

        Booking? charlieBooking = null;
        Booking? daveBooking = null;

        var charlieTask = Task.Run(() =>
        {
            charlieBooking = service.CreateReservation("charlie", VehicleType.SUV,
                "br1", "br2", new DateTime(2025, 7, 22), new DateTime(2025, 7, 25));
        });

        var daveTask = Task.Run(() =>
        {
            daveBooking = service.CreateReservation("dave", VehicleType.Compact,
                "br2", "br2", new DateTime(2025, 7, 22), new DateTime(2025, 7, 24));
        });

        Task.WaitAll(charlieTask, daveTask);

        Console.WriteLine($"\n  Charlie (Mumbai SUV): {(charlieBooking != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"  Dave (Delhi Compact): {(daveBooking != null ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"  Both should succeed (different branches, parallel locks).\n");

        // ── Full lifecycle: pickup + late return ──
        if (charlieBooking != null)
        {
            Console.WriteLine("=== Charlie: Pickup + Late Return ===\n");
            service.PickupVehicle(charlieBooking.Id);
            service.ReturnVehicle(charlieBooking.Id, new DateTime(2025, 7, 27),
                new PaymentProcessor(new CardPayment()));
            Console.WriteLine($"    Final cost: ₹{charlieBooking.TotalCost}");
        }
    }
}
