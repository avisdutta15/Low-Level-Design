public class Ticket
{
    public Guid Id { get; private set; }
    public DateTime EntryTime { get; private set; }
    public Vehicle Vehicle { get; private set; }
    public string ParkingFloorId { get; private set; }
    public string ParkingSpotId { get; private set; }
    public bool ParkingPaid { get; private set; }

    public Ticket(DateTime entryTime, Vehicle vechicle, string parkingFloorId, string parkingSpotId)
    {
        Id = Guid.NewGuid();
        EntryTime = entryTime;
        Vehicle = vechicle;
        ParkingFloorId = parkingFloorId;
        ParkingSpotId = parkingSpotId;
        ParkingPaid = false;
    }
}
