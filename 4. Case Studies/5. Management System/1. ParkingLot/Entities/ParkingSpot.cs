public class ParkingSpot
{
    public string Id { get; set; } = string.Empty;
    public VehicleType VehicleType { get; private set; }
    private int _occupied = 0;

    public ParkingSpot(string id, VehicleType vehicleType)
    {
        this.Id = id;
        this.VehicleType = vehicleType;
    }

    public bool IsOccupied() => Volatile.Read(ref this._occupied) == 1;

    // Not CAS!
    /*
    public bool TryOccupyNotCAS()
    {
        lock (this)
        {
            if (_occupied == false)
            {
                _occupied = true;
                return true;
            }
            return false;
        }
    }
    */

    // CAS loop: atomically sets _occupied 0->1 only if currently 0
    // Retries if another thread changed _occupied between Read and CompareExchange
    public bool TryOccupy()
    {
        while (true)
        {
            int current = Volatile.Read(ref _occupied);
            if (current == 1) return false; // already occupied

            // CompareExchange(ref location, value, comparand)
            // Sets location = value ONLY IF location == comparand, returns original value
            if (Interlocked.CompareExchange(ref _occupied, value: 1, comparand: 0) == 0)
                return true; // we won the race
            // CAS failed: another thread occupied it between Read and CompareExchange -> retry
        }
    }

    // CAS loop: atomically sets _occupied 1->0 only if currently 1
    public bool Vacate()
    {
        while (true)
        {
            int current = Volatile.Read(ref _occupied);
            if (current == 0) return false; // already free

            if (Interlocked.CompareExchange(ref _occupied, value: 0, comparand: 1) == 1)
                return true; // we won the race
            // CAS failed: another thread vacated it between Read and CompareExchange -> retry
        }
    }
}