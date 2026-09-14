package _5_ManagementSystems._1_ParkingLot.entities;

import _5_ManagementSystems._1_ParkingLot.enums.VehicleType;

import java.util.*;
import java.util.concurrent.*;

public class ParkingFloor {
    private final int floorId;
    // private final List<ParkingSpot> parkingSpots;
    private final Map<VehicleType, ConcurrentLinkedQueue<ParkingSpot>> availableParkingSpots;
    public ParkingFloor(int floorId) {
        this.floorId = floorId;
        this.availableParkingSpots = new ConcurrentHashMap<>();
    }

    public void addParkingSpot(ParkingSpot spot){
        availableParkingSpots
                .computeIfAbsent(spot.getVehicleType(), k->new ConcurrentLinkedQueue<>())
                .offer(spot);
    }

    public ParkingSpot findAndPark(Vehicle vehicle){
        Queue<ParkingSpot> parkingSpots = availableParkingSpots.get(vehicle.getVehicleType());
        if(parkingSpots == null)
           return null;

        ParkingSpot spot;
        while((spot = parkingSpots.poll())!=null){
           if(spot.tryOccupy(vehicle))
               return spot;
        }
        return null;
    }

    public void unpark(ParkingSpot parkingSpot){
        parkingSpot.vacate();
        availableParkingSpots
                .get(parkingSpot.getVehicleType())
                .offer(parkingSpot);
    }

    public int getAvailableCount(VehicleType type) {
        ConcurrentLinkedQueue<ParkingSpot> queue = availableParkingSpots.get(type);
        return queue == null ? 0 : queue.size();
    }

    // getters and setters
    public int getFloorId(){
        return this.floorId;
    }
}
