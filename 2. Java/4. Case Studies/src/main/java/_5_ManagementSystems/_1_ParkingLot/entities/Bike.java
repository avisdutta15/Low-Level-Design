package _5_ManagementSystems._1_ParkingLot.entities;

import _5_ManagementSystems._1_ParkingLot.enums.VehicleType;

public class Bike extends Vehicle {
    public Bike(String registrationNum){

        super(registrationNum, VehicleType.BIKE);
    }
}
