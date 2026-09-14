package _5_ManagementSystems._1_ParkingLot.entities;

import _5_ManagementSystems._1_ParkingLot.enums.VehicleType;

public class Car extends Vehicle{
    public Car(String registrationNum){

        super(registrationNum, VehicleType.CAR);
    }
}
