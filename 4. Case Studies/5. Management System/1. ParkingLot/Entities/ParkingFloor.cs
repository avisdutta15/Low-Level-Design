using System.Collections.Concurrent;

public class ParkingFloor
{
    public string Id { get; set; }
    private readonly ConcurrentDictionary<string, ParkingSpot> _parkingSpots;

    public ParkingFloor(string id)
    {
        Id = id;
        _parkingSpots = new ConcurrentDictionary<string, ParkingSpot>();
    }

    public void AddParkingSpot(ParkingSpot parkingSpot)
    {
        // Not Thread-safe
        /*
        if (_parkingSpots.ContainsKey(parkingSpot.Id)) 
        {
            _parkingSpots[parkingSpot.Id] = parkingSpot;
        }
        else
        {
            _parkingSpots.Add(parkingSpot.Id, parkingSpot);
        }
        */

        // Thread-safe
        _parkingSpots.AddOrUpdate(
            key: parkingSpot.Id, 
            addValue: parkingSpot, 
            (key, oldValue) => parkingSpot
        );
    }

    public void RemoveParkingSpot(string parkingSpotId)
    {
        // Not Thread-safe
        /*
        if (_parkingSpots.ContainsKey(parkingSpotId))
        {
            _parkingSpots.Remove(parkingSpotId);
        }
        */

        // Thread-safe
        _parkingSpots.TryRemove(parkingSpotId, out ParkingSpot? removedSpot);
    }

    public ParkingSpot? BookParkingSpot(Vehicle vehicle)
    {
        foreach(KeyValuePair<string, ParkingSpot> spotKV in _parkingSpots)
        {
            var spot = spotKV.Value;
            if(spot.VehicleType == vehicle.Type && spot.TryOccupy() == true)
                return spot;
        }

        return null;
    }

    public Dictionary<VehicleType, int> AvailableSpotsByType()
    {
        var result = new Dictionary<VehicleType, int>();
        foreach (var spot in _parkingSpots.Values)
        {
            if (!spot.IsOccupied())
            {
                if (result.ContainsKey(spot.VehicleType))
                    result[spot.VehicleType]++;
                else
                    result[spot.VehicleType] = 1;
            }
        }
        return result;
    }

    public bool RemoveVehicleFromSpot(string parkingSpotId)
    {
        if (_parkingSpots.ContainsKey(parkingSpotId))
        {
            var spot = _parkingSpots[parkingSpotId];
            return spot.Vacate();
        }
        return false;
    }
}