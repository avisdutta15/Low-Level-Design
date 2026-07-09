var peakHours = new HashSet<int> { 8, 9, 17, 18, 19 }; // morning rush: 8-9, evening rush: 17-19
var rates = new Dictionary<VehicleType, PricingRate>
{
    { VehicleType.CAR,   new PricingRate(PeakRate: 20, NonPeakRate: 10) },
    { VehicleType.BIKE,  new PricingRate(PeakRate: 10, NonPeakRate:  5) },
    { VehicleType.TRUCK, new PricingRate(PeakRate: 40, NonPeakRate: 20) }
};
IPricingStrategy pricingStrategy = new TimeBasedPricing(peakHours, rates);

// Thread Comparison:
// 1. new Thread() + for loop  -> Manual OS-level threads. Full control over lifecycle (Start/Join).
//                                Requires explicit variable capture (threadId) to avoid closure bugs.
//                                Best when you need fine-grained control over thread behavior.
//
// 2. Parallel.ForEach          -> Iterates over a collection concurrently using the ThreadPool.
//                                No lifecycle management needed; blocks until all iterations complete.
//                                Best when you have an existing collection to process in parallel.
//
// 3. Parallel.For              -> Same as Parallel.ForEach but index-based (from/to range).
//                                No closure bug since index is passed as a direct lambda parameter.
//                                Best when iteration count matters more than the data collection.

TestObserverPattern(pricingStrategy);
TestWithForLoop(pricingStrategy);
TestWithParallelForEach(pricingStrategy);
TestWithParallelFor(pricingStrategy);
TestBikeAndTruckParking(pricingStrategy);
TestUnParkWithPricing(pricingStrategy);
TestSubHourStay(pricingStrategy);
TestNoSpotAvailable(pricingStrategy);

static void TestObserverPattern(IPricingStrategy pricingStrategy)
{
    Console.WriteLine("\n--- Observer Pattern Test ---");
    var lot = CreateFreshLot(pricingStrategy);

    var board = new DisplayBoard("Entrance-1");
    var app   = new MobileApp("user-42");
    lot.Subscribe(board);
    lot.Subscribe(app);

    var car  = new Car("CAR-OBS-01");
    var ticket = lot.ParkVehicle(car);

    if (ticket != null)
        lot.UnParkVehicle(ticket, ticket.EntryTime.AddHours(2), PaymentMode.CASH);

    lot.Unsubscribe(app);
    Console.WriteLine("[MobileApp user-42 unsubscribed]");
    lot.ParkVehicle(new Car("CAR-OBS-02")); // only DisplayBoard should fire
}

static ParkingLot CreateFreshLot(IPricingStrategy pricingStrategy)
{
    ParkingFloor floor1 = new ParkingFloor("F1");
    floor1.AddParkingSpot(new ParkingSpot("F1:1", VehicleType.CAR));
    floor1.AddParkingSpot(new ParkingSpot("F1:2", VehicleType.CAR));
    floor1.AddParkingSpot(new ParkingSpot("F1:3", VehicleType.BIKE));
    floor1.AddParkingSpot(new ParkingSpot("F1:4", VehicleType.BIKE));
    floor1.AddParkingSpot(new ParkingSpot("F1:5", VehicleType.TRUCK));
    floor1.AddParkingSpot(new ParkingSpot("F1:6", VehicleType.TRUCK));

    ParkingFloor floor2 = new ParkingFloor("F2");
    floor2.AddParkingSpot(new ParkingSpot("F2:1", VehicleType.CAR));
    floor2.AddParkingSpot(new ParkingSpot("F2:2", VehicleType.CAR));
    floor2.AddParkingSpot(new ParkingSpot("F2:3", VehicleType.BIKE));
    floor2.AddParkingSpot(new ParkingSpot("F2:4", VehicleType.BIKE));
    floor2.AddParkingSpot(new ParkingSpot("F2:5", VehicleType.TRUCK));
    floor2.AddParkingSpot(new ParkingSpot("F2:6", VehicleType.TRUCK));

    ParkingLot lot = new ParkingLot("PL001", pricingStrategy);
    lot.AddParkingFloor(floor1);
    lot.AddParkingFloor(floor2);
    return lot;
}

// Manual OS-level threads via for loop. Requires Start()/Join() and explicit variable capture.
static void TestWithForLoop(IPricingStrategy pricingStrategy)
{
    Console.WriteLine("\n--- new Thread() + for loop Test ---");
    var lot = CreateFreshLot(pricingStrategy); // 4 CAR spots, 6 threads -> 2 should be blocked
    var threads = new Thread[6];
    for (int i = 0; i < threads.Length; i++)
    {
        int threadId = i + 1; // capture loop variable
        threads[i] = new Thread(() =>
        {
            var car = new Car($"CAR-{threadId:D2}");
            var ticket = lot.ParkVehicle(car);
            Console.WriteLine(ticket != null
                ? $"[Thread {threadId}] {car.Number} booked spot {ticket.ParkingSpotId} on floor {ticket.ParkingFloorId}"
                : $"[Thread {threadId}] {car.Number} -> No spot available (blocked correctly)");
        });
    }
    for (int i = 0; i < threads.Length; i++) threads[i].Start();
    for (int i = 0; i < threads.Length; i++) threads[i].Join();
}

// ThreadPool-based parallelism over a collection. No lifecycle management needed.
static void TestWithParallelForEach(IPricingStrategy pricingStrategy)
{
    Console.WriteLine("\n--- Parallel.ForEach Test ---");
    var lot = CreateFreshLot(pricingStrategy); // fresh lot, 4 CAR spots, 6 vehicles -> 2 blocked
    var carNumbers = new List<string> { "CAR-07", "CAR-08", "CAR-09", "CAR-10", "CAR-11", "CAR-12" };
    Parallel.ForEach(carNumbers, carNumber =>
    {
        var car = new Car(carNumber);
        var ticket = lot.ParkVehicle(car);
        Console.WriteLine(ticket != null
            ? $"[{carNumber}] booked spot {ticket.ParkingSpotId} on floor {ticket.ParkingFloorId}"
            : $"[{carNumber}] -> No spot available (blocked correctly)");
    });
}

// ThreadPool-based parallelism over an index range. Index passed directly, no closure bug.
static void TestWithParallelFor(IPricingStrategy pricingStrategy)
{
    Console.WriteLine("\n--- Parallel.For Test ---");
    var lot = CreateFreshLot(pricingStrategy); // fresh lot, 4 CAR spots, 6 vehicles -> 2 blocked
    Parallel.For(1, 7, i =>
    {
        var car = new Car($"CAR-{i:D2}");
        var ticket = lot.ParkVehicle(car);
        Console.WriteLine(ticket != null
            ? $"[CAR-{i:D2}] booked spot {ticket.ParkingSpotId} on floor {ticket.ParkingFloorId}"
            : $"[CAR-{i:D2}] -> No spot available (blocked correctly)");
    });
}

// Validates that BIKE and TRUCK spots are booked independently of CAR spots
static void TestBikeAndTruckParking(IPricingStrategy pricingStrategy)
{
    Console.WriteLine("\n--- Bike and Truck Parking Test ---");
    var lot = CreateFreshLot(pricingStrategy);

    var bike1 = new Bike("BIKE-01");
    var bike2 = new Bike("BIKE-02");
    var bike3 = new Bike("BIKE-03"); // should be blocked, only 4 BIKE spots but testing per-type isolation

    var truck1 = new Truck("TRUCK-01");
    var truck2 = new Truck("TRUCK-02");

    foreach (var vehicle in new Vehicle[] { bike1, bike2, bike3, truck1, truck2 })
    {
        var ticket = lot.ParkVehicle(vehicle);
        Console.WriteLine(ticket != null
            ? $"{vehicle.Number} booked spot {ticket.ParkingSpotId} on floor {ticket.ParkingFloorId}"
            : $"{vehicle.Number} -> No spot available");
    }
}

// Validates full park -> unpark flow with pricing output
static void TestUnParkWithPricing(IPricingStrategy pricingStrategy)
{
    Console.WriteLine("\n--- UnPark with Pricing Test ---");
    var lot = CreateFreshLot(pricingStrategy);

    var car = new Car("CAR-99");
    var ticket = lot.ParkVehicle(car);
    if (ticket == null) { Console.WriteLine("Parking failed."); return; }

    // Simulate 4 hours of parking from entry time: covers non-peak and peak hours
    DateTime exitTime = ticket.EntryTime.AddHours(4);
    lot.UnParkVehicle(ticket, exitTime, PaymentMode.CASH);
}

// Validates that a sub-hour stay is charged as 1 full hour (fix for issue #8)
static void TestSubHourStay(IPricingStrategy pricingStrategy)
{
    Console.WriteLine("\n--- Sub-Hour Stay Test (minimum 1 hour charge) ---");
    var lot = CreateFreshLot(pricingStrategy);

    var car = new Car("CAR-SUB");
    var ticket = lot.ParkVehicle(car);
    if (ticket == null) { Console.WriteLine("Parking failed."); return; }

    // Exits 30 minutes after entry -> should charge 1 full hour (sub-hour rounded up)
    DateTime exitTime = ticket.EntryTime.AddMinutes(30);
    lot.UnParkVehicle(ticket, exitTime, PaymentMode.CASH);
}

// Validates that booking fails gracefully when all spots of a type are full
static void TestNoSpotAvailable(IPricingStrategy pricingStrategy)
{
    Console.WriteLine("\n--- No Spot Available Test ---");
    var lot = CreateFreshLot(pricingStrategy); // only 2 TRUCK spots per floor = 4 total

    for (int i = 1; i <= 5; i++) // 5 trucks, last one should be blocked
    {
        var truck = new Truck($"TRUCK-{i:D2}");
        var ticket = lot.ParkVehicle(truck);
        Console.WriteLine(ticket != null
            ? $"{truck.Number} booked spot {ticket.ParkingSpotId} on floor {ticket.ParkingFloorId}"
            : $"{truck.Number} -> No spot available (all {4} TRUCK spots occupied)");
    }
}




// ParkingLot -> A Parking Lot is made up of Parking Floors
//               Dictionary <id, ParkingFloor>
//               AddParkingFloor()
//               RemoveParkingFloor()
//               BookParkingSpot(Vehicle, EntryTime)   -> Books a Parking Spot and returns a ticket if successful
//                   Iterate through the Parking Floors
//                   if(ParkingFloor.BookParkingSpot(Vehicle) != null)
//                       return ParkingSpot;
//                   return null;
//               UnParkVehicle(TicketId, ExitTime, PaymentMode)    -> The driver hands over the ticket and the payment mode
//                   if the ticket is null
//                      return false;
//                   calculate the total amount using PricingStrategy 
//                   make the payment using PaymentStrategy.GetPaymentStrategy(PaymentMode).Pay(amount)
//                      if payment fails
//                          return false;
//                   free the spot
//                   remove the ticket
//                   return true;
// ParkingFloor -> A Parking Floor is made up of Parking Spots
//                  Dictionary <id, ParkingSpot>
//                  AddParkingSpot()
//                  RemoveParkingSpot()
//                  BookParkingSpot(Vehicle)
//                      Iterate through the Parking Spots
//                      if(ParkingSpot.GetVehicleType() == Vehicle.Type() and ParkingSpot.TryOccupy())
//                          return ParkingSpot;
//                      return null;
// ParkingSpot -> A Parking Spot can park a Vehicle
//                Vehicle
//                IsOccupied()
//                GetVehicleType()
//                TryOccupy()
// Vehicle -> Base Class that has concrete implementations
