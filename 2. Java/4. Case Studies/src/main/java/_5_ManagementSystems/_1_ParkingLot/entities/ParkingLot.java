package _5_ManagementSystems._1_ParkingLot.entities;

import _5_ManagementSystems._1_ParkingLot.enums.PaymentStatus;
import _5_ManagementSystems._1_ParkingLot.enums.VehicleType;
import _5_ManagementSystems._1_ParkingLot.entities.payment.PaymentResult;
import _5_ManagementSystems._1_ParkingLot.observer.EventType;
import _5_ManagementSystems._1_ParkingLot.observer.ParkingEvent;
import _5_ManagementSystems._1_ParkingLot.observer.ParkingObserver;
import _5_ManagementSystems._1_ParkingLot.strategies.payment.PaymentModeStrategy;
import _5_ManagementSystems._1_ParkingLot.strategies.payment.PaymentProcessor;
import _5_ManagementSystems._1_ParkingLot.strategies.pricing.ParkingFeeStrategy;

import java.time.LocalDateTime;
import java.util.*;
import java.util.concurrent.*;

public class ParkingLot {
    private static volatile ParkingLot instance;
    private static final Object lock = new Object();

    private final List<ParkingFloor> floors = new CopyOnWriteArrayList<>();
    private final Map<String, Ticket> parkingTickets = new ConcurrentHashMap<>();
    private final ParkingFeeStrategy parkingFeeStrategy;
    private final PaymentProcessor paymentProcessor;
    private final List<ParkingObserver> observers = new CopyOnWriteArrayList<>();

    private ParkingLot(ParkingFeeStrategy parkingFeeStrategy, PaymentProcessor paymentProcessor){
        this.parkingFeeStrategy = parkingFeeStrategy;
        this.paymentProcessor = paymentProcessor;
    }

    public static ParkingLot getInstance(ParkingFeeStrategy parkingFeeStrategy, PaymentProcessor paymentProcessor){
        if(instance == null){
            synchronized (lock){
                if(instance == null){
                    instance = new ParkingLot(parkingFeeStrategy, paymentProcessor);
                }
            }
        }
        return instance;
    }

    public void addFloor(ParkingFloor floor){
        floors.add(floor);
    }

    public void addParkingSpot(int floorId, ParkingSpot spot){
        // validate the floor exists
        ParkingFloor floor = floors.stream()
                .filter(f -> f.getFloorId() == floorId)
                .findFirst()
                .orElseThrow(()->new IllegalArgumentException("Floor not found : " + floorId));

        /*  Alternative for loop
            ParkingFloor floor = null;
            for (ParkingFloor f : floors) {
                if (f.getFloorId() == floorId) {
                    floor = f;
                    break;
                }
            }
            if (floor == null) {
                throw new IllegalArgumentException("Floor not found : " + floorId);
            }
        */

        floor.addParkingSpot(spot); // add the spot to the floor
        notifyObservers(new ParkingEvent(spot.getVehicleType(), EventType.SPOT_ADDED));
    }

    public Ticket park(Vehicle vehicle){
        ParkingSpot spot;
        for(ParkingFloor floor : floors){   // for each floor,
            spot = floor.findAndPark(vehicle);  // try to park on this floor
            if(spot!=null){
                Ticket ticket= new Ticket(floor, spot);     // generate ticket
                parkingTickets.put(ticket.getId(), ticket); // store ticket
                notifyObservers(new ParkingEvent(vehicle.getVehicleType(), EventType.PARKED));
                return ticket;  // return ticket
            }
        }
        throw new RuntimeException("Parking is Full");
    }

    public void unPark(String ticketId, PaymentModeStrategy paymentModeStrategy){
        // remove(key) returns previous value, null if key not present
        // remove(key) is atomic and better than get(key) and then remove(key).
        Ticket ticket = parkingTickets.remove(ticketId);
        if(ticket == null)
            throw new RuntimeException("Invalid Ticket Exception");
        ticket.setExitTime(LocalDateTime.now());    // set the exit time

        double fee = parkingFeeStrategy.calculateFee(ticket);   // calculate the parking fee
        PaymentResult result = paymentProcessor.processPayment(paymentModeStrategy, ticketId, fee); //process payment
        if(result.getStatus().equals(PaymentStatus.SUCCESS)){   // if payment is successful
            VehicleType type = ticket.getSpot().getVehicleType();   // get the vehicle type from ticket
            ticket.getFloor().unpark(ticket.getSpot());             // call floor.unpark(spot) from ticket
            notifyObservers(new ParkingEvent(type, EventType.UNPARKED));    // fire event
        }
        else{
            ticket.setExitTime(null);       // payment not successful. nullify exit time.
            parkingTickets.put(ticketId, ticket);  // put it back on failure
        }
    }

    // ===== Observer Pattern =====
    public void subscribe(ParkingObserver observer) {
        observers.add(observer);
    }

    public void unsubscribe(ParkingObserver observer) {
        observers.remove(observer);
    }

    private void notifyObservers(ParkingEvent event) {
        for (ParkingObserver observer : observers) {
            observer.onEvent(event);
        }
    }
}
