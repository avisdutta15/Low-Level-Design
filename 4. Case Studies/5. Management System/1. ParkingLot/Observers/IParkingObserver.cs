public interface IParkingObserver
{
    void Update(Dictionary<VehicleType, int> availableByType);
}
