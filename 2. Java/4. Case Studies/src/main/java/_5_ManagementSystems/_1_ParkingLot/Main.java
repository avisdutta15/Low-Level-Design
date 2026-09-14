package _5_ManagementSystems._1_ParkingLot;

import _5_ManagementSystems._1_ParkingLot.enums.VehicleType;
import _5_ManagementSystems._1_ParkingLot.entities.*;
import _5_ManagementSystems._1_ParkingLot.observer.*;
import _5_ManagementSystems._1_ParkingLot.strategies.payment.*;
import _5_ManagementSystems._1_ParkingLot.strategies.pricing.*;

import java.util.concurrent.*;

public class Main {
    public static void main(String[] args) throws InterruptedException {
        ParkingFeeStrategy pricingStrategy = new EventBasedPricing();
        PaymentProcessor paymentProcessor = new PaymentProcessor();

        // Subscribe dashboard observer (async + ordered)
        ParkingLot lot = ParkingLot.getInstance(pricingStrategy, paymentProcessor);
        DashboardDisplay dashboard = new DashboardDisplay("Entrance-1");
        lot.subscribe(new AsyncParkingObserver(dashboard));

        testBasicParkUnpark(pricingStrategy, paymentProcessor);
        testBikeAndTruckParking(pricingStrategy, paymentProcessor);
        testNoSpotAvailable(pricingStrategy, paymentProcessor);
        testSubHourStay(pricingStrategy, paymentProcessor);
        testThreadsWithForLoop(pricingStrategy, paymentProcessor);
        testWithExecutorService(pricingStrategy, paymentProcessor);
        testConcurrentUnparkRace(pricingStrategy, paymentProcessor);

        // Give async observers time to flush
        Thread.sleep(1000);
    }

    private static ParkingLot createFreshLot(ParkingFeeStrategy pricingStrategy, PaymentProcessor paymentProcessor) {
        // Use reflection-free approach: create a new lot each time for test isolation
        // Since ParkingLot is singleton, we'll work with floors added per test
        ParkingLot lot = ParkingLot.getInstance(pricingStrategy, paymentProcessor);
        return lot;
    }

    private static void testBasicParkUnpark(ParkingFeeStrategy pricingStrategy, PaymentProcessor paymentProcessor) throws InterruptedException {
        System.out.println("\n--- Basic Park/Unpark Test (verify spot returned to queue) ---");
        ParkingLot lot = ParkingLot.getInstance(pricingStrategy, paymentProcessor);

        ParkingFloor floor1 = new ParkingFloor(1);
        lot.addFloor(floor1);
        lot.addParkingSpot(1, new ParkingSpot("1A", VehicleType.CAR));
        lot.addParkingSpot(1, new ParkingSpot("1B", VehicleType.CAR));
        lot.addParkingSpot(1, new ParkingSpot("1C", VehicleType.BIKE));

        // Fill both car spots
        Ticket t1 = lot.park(new Car("KA-01-AB-1234"));
        Ticket t2 = lot.park(new Car("KA-02-CD-5678"));
        System.out.println("Parked at: " + t1.getSpot().getId() + ", " + t2.getSpot().getId());

        Thread.sleep(1000);

        // Unpark one
        PaymentModeStrategy payment = new CreditCardPayment("4111111111111111", "12/26", "123");
        lot.unPark(t1.getId(), payment);
        System.out.println("Unparked " + t1.getSpot().getId());

        // This succeeds only if the spot was returned to the queue
        Ticket t3 = lot.park(new Car("KA-03-EF-9999"));
        System.out.println("Spot returned to queue — parked at: " + t3.getSpot().getId());
    }

    private static void testBikeAndTruckParking(ParkingFeeStrategy pricingStrategy, PaymentProcessor paymentProcessor) {
        System.out.println("\n--- Bike and Truck Parking Test ---");
        ParkingLot lot = ParkingLot.getInstance(pricingStrategy, paymentProcessor);

        ParkingFloor floor2 = new ParkingFloor(2);
        lot.addFloor(floor2);
        lot.addParkingSpot(2, new ParkingSpot("2A", VehicleType.BIKE));
        lot.addParkingSpot(2, new ParkingSpot("2B", VehicleType.BIKE));
        lot.addParkingSpot(2, new ParkingSpot("2C", VehicleType.TRUCK));

        Vehicle bike1 = new Bike("KA-01-EF-1111");
        Vehicle bike2 = new Bike("KA-01-EF-2222");
        Vehicle truck1 = new Truck("KA-01-GH-3333");

        System.out.println(bike1.getRegistrationNum() + " parked: " + (lot.park(bike1) != null));
        System.out.println(bike2.getRegistrationNum() + " parked: " + (lot.park(bike2) != null));
        System.out.println(truck1.getRegistrationNum() + " parked: " + (lot.park(truck1) != null));

        // Try parking a car on floor with no car spots
        try {
            Vehicle car = new Car("KA-01-XX-9999");
            lot.park(car);  // should only find spots from floor1 (which may be full)
            System.out.println(car.getRegistrationNum() + " parked (found spot on another floor)");
        } catch (RuntimeException e) {
            System.out.println("Car parking failed as expected: " + e.getMessage());
        }
    }

    private static void testNoSpotAvailable(ParkingFeeStrategy pricingStrategy, PaymentProcessor paymentProcessor) {
        System.out.println("\n--- No Spot Available Test ---");
        ParkingLot lot = ParkingLot.getInstance(pricingStrategy, paymentProcessor);

        ParkingFloor floor3 = new ParkingFloor(3);
        lot.addFloor(floor3);
        lot.addParkingSpot(3, new ParkingSpot("3A", VehicleType.TRUCK));

        Vehicle truck1 = new Truck("MH-01-AA-0001");
        Vehicle truck2 = new Truck("MH-01-AA-0002");

        Ticket t = lot.park(truck1);
        System.out.println(truck1.getRegistrationNum() + " parked: " + (t != null));

        try {
            lot.park(truck2);
            System.out.println(truck2.getRegistrationNum() + " parked (unexpected)");
        } catch (RuntimeException e) {
            System.out.println(truck2.getRegistrationNum() + " blocked correctly: " + e.getMessage());
        }
    }

    private static void testSubHourStay(ParkingFeeStrategy pricingStrategy, PaymentProcessor paymentProcessor) throws InterruptedException {
        System.out.println("\n--- Sub-Hour Stay Test (minimum 1 hour charge) ---");
        ParkingLot lot = ParkingLot.getInstance(pricingStrategy, paymentProcessor);

        ParkingFloor floor4 = new ParkingFloor(4);
        lot.addFloor(floor4);
        lot.addParkingSpot(4, new ParkingSpot("4A", VehicleType.CAR));

        Vehicle car = new Car("DL-01-SUB-0001");
        Ticket ticket = lot.park(car);
        System.out.println(car.getRegistrationNum() + " parked");

        // Exit almost immediately — should still charge minimum 1 hour
        Thread.sleep(100);

        PaymentModeStrategy upi = new UPIPayment("subhour@upi");
        lot.unPark(ticket.getId(), upi);
        System.out.println("Unparked after sub-hour stay (charged minimum 1 hour)");
    }

    // Manual threads via for loop — similar to C# new Thread() approach
    private static void testThreadsWithForLoop(ParkingFeeStrategy pricingStrategy, PaymentProcessor paymentProcessor) throws InterruptedException {
        System.out.println("\n--- Thread + For Loop Test (6 threads, 2 CAR spots) ---");
        ParkingLot lot = ParkingLot.getInstance(pricingStrategy, paymentProcessor);

        ParkingFloor floor5 = new ParkingFloor(5);
        lot.addFloor(floor5);
        lot.addParkingSpot(5, new ParkingSpot("5A", VehicleType.CAR));
        lot.addParkingSpot(5, new ParkingSpot("5B", VehicleType.CAR));

        Thread[] threads = new Thread[6];
        for (int i = 0; i < threads.length; i++) {
            int threadId = i + 1;
            threads[i] = new Thread(() -> {
                Vehicle car = new Car("TH-" + String.format("%02d", threadId));
                try {
                    Ticket ticket = lot.park(car);
                    System.out.println("[Thread-" + threadId + "] " + car.getRegistrationNum() + " parked at " + ticket.getSpot().getId());
                } catch (RuntimeException e) {
                    System.out.println("[Thread-" + threadId + "] " + car.getRegistrationNum() + " -> " + e.getMessage());
                }
            });
        }

        for (Thread t : threads) t.start();
        for (Thread t : threads) t.join();
    }

    // ExecutorService — similar to C# Parallel.For / ThreadPool
    private static void testWithExecutorService(ParkingFeeStrategy pricingStrategy, PaymentProcessor paymentProcessor) throws InterruptedException {
        System.out.println("\n--- ExecutorService Test (5 threads, 2 BIKE spots) ---");
        ParkingLot lot = ParkingLot.getInstance(pricingStrategy, paymentProcessor);

        ParkingFloor floor6 = new ParkingFloor(6);
        lot.addFloor(floor6);
        lot.addParkingSpot(6, new ParkingSpot("6A", VehicleType.BIKE));
        lot.addParkingSpot(6, new ParkingSpot("6B", VehicleType.BIKE));

        ExecutorService executor = Executors.newFixedThreadPool(5);
        for (int i = 1; i <= 5; i++) {
            int id = i;
            executor.submit(() -> {
                Vehicle bike = new Bike("EX-BIKE-" + String.format("%02d", id));
                try {
                    Ticket ticket = lot.park(bike);
                    System.out.println("[Pool-" + id + "] " + bike.getRegistrationNum() + " parked at " + ticket.getSpot().getId());
                } catch (RuntimeException e) {
                    System.out.println("[Pool-" + id + "] " + bike.getRegistrationNum() + " -> " + e.getMessage());
                }
            });
        }
        executor.shutdown();
        executor.awaitTermination(5, TimeUnit.SECONDS);
    }

    // Two threads race to unpark the same ticket — only one should succeed
    private static void testConcurrentUnparkRace(ParkingFeeStrategy pricingStrategy, PaymentProcessor paymentProcessor) throws InterruptedException {
        System.out.println("\n--- Concurrent Unpark Race Test ---");
        ParkingLot lot = ParkingLot.getInstance(pricingStrategy, paymentProcessor);

        ParkingFloor floor7 = new ParkingFloor(7);
        lot.addFloor(floor7);
        lot.addParkingSpot(7, new ParkingSpot("7A", VehicleType.CAR));

        Vehicle car = new Car("RACE-CAR-01");
        Ticket ticket = lot.park(car);
        System.out.println(car.getRegistrationNum() + " parked, ticket: " + ticket.getId());

        Thread.sleep(500);

        Thread t1 = new Thread(() -> {
            try {
                lot.unPark(ticket.getId(), new UPIPayment("racer1@upi"));
                System.out.println("[Thread-1] Unparked successfully");
            } catch (RuntimeException e) {
                System.out.println("[Thread-1] Failed: " + e.getMessage());
            }
        });

        Thread t2 = new Thread(() -> {
            try {
                lot.unPark(ticket.getId(), new CreditCardPayment("4222222222222222", "01/27", "456"));
                System.out.println("[Thread-2] Unparked successfully");
            } catch (RuntimeException e) {
                System.out.println("[Thread-2] Failed: " + e.getMessage());
            }
        });

        t1.start();
        t2.start();
        t1.join();
        t2.join();

        System.out.println("Only one thread should have succeeded above.");
    }
}
