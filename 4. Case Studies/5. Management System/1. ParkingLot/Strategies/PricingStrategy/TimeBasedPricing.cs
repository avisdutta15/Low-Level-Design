public record PricingRate(double PeakRate, double NonPeakRate);
public class TimeBasedPricing : IPricingStrategy
{
    // Explicit list of peak hours in 24h format e.g. 8,9 = morning rush, 17,18,19 = evening rush
    private readonly HashSet<int> _peakHours;
    private readonly Dictionary<VehicleType, PricingRate> _rates;

    public TimeBasedPricing(HashSet<int> peakHours, Dictionary<VehicleType, PricingRate> rates)
    {
        _peakHours = peakHours;
        _rates = rates;
    }

    public double CalculateFee(Vehicle vehicle, DateTime entryTime, DateTime exitTime)
    {
        if (!_rates.TryGetValue(vehicle.Type, out var rate))
            throw new ArgumentException($"No pricing rate defined for vehicle type {vehicle.Type}");

        int peakHoursCount = 0;
        int nonPeakHoursCount = 0;

        DateTime current = entryTime;
        // Round up exitTime to ensure sub-hour stays are charged as at least 1 hour
        DateTime effectiveExitTime = exitTime.Minute > 0 || exitTime.Second > 0
            ? exitTime.Date.AddHours(exitTime.Hour + 1)
            : exitTime;

        while (current < effectiveExitTime)
        {
            if (_peakHours.Contains(current.Hour))
                peakHoursCount++;
            else
                nonPeakHoursCount++;

            current = current.AddHours(1);
        }

        double totalFee = (peakHoursCount * rate.PeakRate) + (nonPeakHoursCount * rate.NonPeakRate);

        Console.WriteLine($"Vehicle: {vehicle.Number} | Peak Hours: {peakHoursCount} @ {rate.PeakRate}/hr" +
                          $" | Non-Peak Hours: {nonPeakHoursCount} @ {rate.NonPeakRate}/hr | Total: {totalFee:C}");

        return totalFee;
    }
}
