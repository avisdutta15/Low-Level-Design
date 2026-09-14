package _5_ManagementSystems._1_ParkingLot.strategies.pricing;

import _5_ManagementSystems._1_ParkingLot.entities.Ticket;

public interface ParkingFeeStrategy {
    public double calculateFee(Ticket ticket);
}
