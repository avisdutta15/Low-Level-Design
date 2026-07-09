public interface IPricingStrategy
{
    public double CalculateFee(Vehicle vehicle, DateTime entryTime, DateTime exitTime);
}