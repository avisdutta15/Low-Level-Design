public class MobileApp : IParkingObserver
{
    private readonly string _userId;

    public MobileApp(string userId) => _userId = userId;

    public void Update(Dictionary<VehicleType, int> availableByType)
    {
        Console.WriteLine($"[MobileApp {_userId}] Push notification:");
        foreach (var kvp in availableByType)
            Console.WriteLine($"  {kvp.Key}: {kvp.Value} spots available");
    }
}
