package _5_ManagementSystems._1_ParkingLot.observer;

import _5_ManagementSystems._1_ParkingLot.enums.VehicleType;

public record ParkingEvent(VehicleType vehicleType, EventType eventType) {}
