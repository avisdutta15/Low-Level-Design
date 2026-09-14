package _5_ManagementSystems._1_ParkingLot.entities;

import _5_ManagementSystems._1_ParkingLot.enums.VehicleType;

public class Truck extends Vehicle {
    public Truck(String registrationNum) {
        super(registrationNum, VehicleType.TRUCK);
    }
}
