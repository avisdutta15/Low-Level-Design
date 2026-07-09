
public class EventBasedPricing : IPricingStrategy
{
    private readonly Dictionary<VehicleType, double> _rates;

    // Event pricing: Higher per-hour rates
    public EventBasedPricing()
    {
        _rates = new Dictionary<VehicleType, double>
        {
            { VehicleType.CAR, 20.0 },
            { VehicleType.TRUCK, 30.0 },
            { VehicleType.BIKE, 40.0 }
        };
    }
    public double CalculateFee(Vehicle vehicle, DateTime entryTime, DateTime exitTime)
    {
        var totalHours = (exitTime - entryTime).TotalHours;
        return totalHours * _rates[vehicle.Type];
    }
}
