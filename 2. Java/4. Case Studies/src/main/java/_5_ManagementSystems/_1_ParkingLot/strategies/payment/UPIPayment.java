package _5_ManagementSystems._1_ParkingLot.strategies.payment;

import _5_ManagementSystems._1_ParkingLot.enums.PaymentStatus;
import _5_ManagementSystems._1_ParkingLot.entities.payment.PaymentResult;

import java.util.UUID;

public class UPIPayment implements PaymentModeStrategy {
    private final String upiId;

    public UPIPayment(String upiId) {
        this.upiId = upiId;
    }

    @Override
    public boolean validate() {
        return upiId != null && upiId.matches("[a-zA-Z0-9.]+@[a-zA-Z]+");
    }

    @Override
    public PaymentResult pay(double amount) {
        if (!validate()) {
            return new PaymentResult(PaymentStatus.FAILED, null, "Invalid UPI ID");
        }
        String txnId = UUID.randomUUID().toString();
        System.out.println("Requesting ₹" + amount + " via UPI to " + upiId);
        return new PaymentResult(PaymentStatus.SUCCESS, txnId, "UPI payment successful");
    }

    @Override
    public PaymentResult refund(String transactionId, double amount) {
        System.out.println("Refunding ₹" + amount + " to UPI " + upiId);
        return new PaymentResult(PaymentStatus.SUCCESS, transactionId, "UPI refund processed");
    }
}
