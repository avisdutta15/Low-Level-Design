package _5_ManagementSystems._1_ParkingLot.strategies.payment;

import _5_ManagementSystems._1_ParkingLot.entities.payment.PaymentResult;

public interface PaymentModeStrategy {
    boolean validate();
    PaymentResult pay(double amount);
    PaymentResult refund(String transactionId, double amount);
}
