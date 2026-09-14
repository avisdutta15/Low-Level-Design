package _5_ManagementSystems._1_ParkingLot.entities;

import _5_ManagementSystems._1_ParkingLot.enums.VehicleType;

import java.util.concurrent.atomic.AtomicBoolean;

public class ParkingSpot {
    private final String id;
    private final VehicleType vehicleType;
    private volatile Vehicle vehicle;
    private final AtomicBoolean isOccupied;

    public ParkingSpot(String id, VehicleType vehicleType) {
        this.id = id;
        this.vehicleType = vehicleType;
        this.isOccupied = new AtomicBoolean(false);
    }

    public String getId() {
        return id;
    }

    public VehicleType getVehicleType() {
        return vehicleType;
    }

    public boolean getIsOccupied() {
        return isOccupied.get();
    }

    public boolean tryOccupy(Vehicle vehicle){
        if(isOccupied.compareAndSet(false, true) == true){
            this.vehicle = vehicle;
            return true;
        }
        return false;
    }

    public boolean vacate(){
        if(isOccupied.compareAndSet(true, false) == true){
            this.vehicle = null;
            return true;
        }
        return false;
    }
}
