package _5_ManagementSystems._1_ParkingLot.entities;

import _5_ManagementSystems._1_ParkingLot.enums.VehicleType;

public abstract class Vehicle {
    private final  String registrationNum;
    private final VehicleType vehicleType;

    public Vehicle(String registrationNum, VehicleType vehicleType){
        this.registrationNum = registrationNum;
        this.vehicleType = vehicleType;
    }

    public String getRegistrationNum() {
        return registrationNum;
    }

    public VehicleType getVehicleType() {
        return vehicleType;
    }
}
