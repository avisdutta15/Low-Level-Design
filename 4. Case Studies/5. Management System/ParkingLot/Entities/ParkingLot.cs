using System.Collections.Concurrent;

public class ParkingLot : IParkingSubject
{
    public string Id { get; set; }
    private readonly ConcurrentDictionary<string, ParkingFloor> _parkingFloors;
    private readonly ConcurrentDictionary<Guid, Ticket> _activeTickets;
    private readonly List<IParkingObserver> _observers;
    private readonly object _observerLock = new();
    private readonly IPricingStrategy _pricingStrategy;

    public ParkingLot(string id, IPricingStrategy pricingStrategy)
    {
        Id = id;
        _parkingFloors = new ConcurrentDictionary<string, ParkingFloor>();
        _activeTickets = new ConcurrentDictionary<Guid, Ticket>();
        _pricingStrategy = pricingStrategy;
        _observers = new();
    }

    public void Subscribe(IParkingObserver observer)
    {
        lock (_observerLock)
        {
            if (!_observers.Contains(observer))
                _observers.Add(observer);
        }
    }

    public void Unsubscribe(IParkingObserver observer)
    {
        lock (_observerLock)
        {
            _observers.Remove(observer);
        }
    }

    public void Notify()
    {
        var combined = new Dictionary<VehicleType, int>();

        // Aggregate available spots from all floors
        foreach (var floor in _parkingFloors.Values)
        {
            foreach (var kvp in floor.AvailableSpotsByType())
            {
                if (combined.ContainsKey(kvp.Key))
                    combined[kvp.Key] += kvp.Value;
                else
                    combined[kvp.Key] = kvp.Value;
            }
        }

        List<IParkingObserver> snapshot = new();
        lock (_observerLock)
        {
            snapshot = _observers.ToList();
        }

        foreach (var observer in snapshot)
            observer.Update(combined);
    }

    public void AddParkingFloor(ParkingFloor parkingFloor)
    {
        // Not Thread-safe
        /*
        if (_parkingFloors.ContainsKey(parkingFloor.Id))
        {
            _parkingFloors[parkingFloor.Id] = parkingFloor;
        }
        else
        {
            _parkingFloors.Add(parkingFloor.Id, parkingFloor);
        }
        */

        // Thread-safe
        _parkingFloors.AddOrUpdate(
            key: parkingFloor.Id,
            addValue: parkingFloor,
            updateValueFactory: (key, oldValue) => parkingFloor
        );
    }

    public bool RemoveParkingFloor(ParkingFloor parkingFloor) 
    {
        // Not Thread-safe
        /*
        if (_parkingFloors.ContainsKey(parkingFloor.Id))
        {
            _parkingFloors.Remove(parkingFloor.Id);
            return true;
        }
        return false;
        */

        // Thread-safe
        return _parkingFloors.TryRemove(parkingFloor.Id, out ParkingFloor? _);
    }

    public Ticket? ParkVehicle(Vehicle vehicle)
    {
        foreach (var floor in _parkingFloors.Values)
        {
            var spot = floor.BookParkingSpot(vehicle);
            if (spot != null)
            {
                // Successfully reserved the spot via atomic operation
                Ticket ticket = new Ticket(DateTime.Now, vehicle, floor.Id, spot.Id);
                _activeTickets.AddOrUpdate(
                    key: ticket.Id,
                    addValue: ticket,
                    updateValueFactory: (key, oldValue) => ticket
                );
                Console.WriteLine($"Vehicle parked. Ticket: {ticket.Id}");
                Notify();
                return ticket;
            }
        }

        Console.WriteLine($"No spot available for vehicle type: {vehicle.Type}");
        return null;
    }

    public bool UnParkVehicle(Ticket ticket, DateTime exitTime, PaymentMode paymentMode)
    {
        // Validate the ticket
        if(ticket == null || _activeTickets.ContainsKey(ticket.Id) == false)
        {
            Console.WriteLine("Invalid ticket ID.");
            return false;
        }

        // Calculate the parking fee
        double parkingFee = _pricingStrategy.CalculateFee(ticket.Vehicle, ticket.EntryTime, exitTime);

        // Process the payment
        IPaymentStrategy paymentStrategy = PaymentStrategyFactory.GetStrategy(paymentMode);
        bool paid = paymentStrategy.Pay(ticket, parkingFee);
        if (!paid)
        {
            Console.WriteLine("Vehicle cannot exit. Payment unsuccessful.");
            return false;
        }

        // Mark the spot as empty and remove the ticket
        var success = _parkingFloors[ticket.ParkingFloorId].RemoveVehicleFromSpot(ticket.ParkingSpotId);
        if (success)
        {
            _activeTickets.TryRemove(ticket.Id, out Ticket? _);
            Console.WriteLine($"Vehicle exited. Fee charged: ₹{parkingFee}");
            Notify();
            return true;
        }
        
        return false;
    }
}