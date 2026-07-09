public class DisplayBoard : IParkingObserver
{
    private readonly string _boardId;

    public DisplayBoard(string boardId) => _boardId = boardId;

    public void Update(Dictionary<VehicleType, int> availableByType)
    {
        Console.WriteLine($"[DisplayBoard {_boardId}]");
        foreach (var kvp in availableByType)
            Console.WriteLine($"  {kvp.Key}: {kvp.Value} spots available");
    }
}
