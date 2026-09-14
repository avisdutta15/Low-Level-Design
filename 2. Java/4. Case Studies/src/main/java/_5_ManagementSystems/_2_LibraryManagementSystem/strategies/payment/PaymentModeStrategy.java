package _5_ManagementSystems._2_LibraryManagementSystem.strategies.payment;

import _5_ManagementSystems._2_LibraryManagementSystem.entities.payment.PaymentResult;

public interface PaymentModeStrategy {
    boolean validate();
    PaymentResult pay(double amount);
    PaymentResult refund(String transactionId, double amount);
}
