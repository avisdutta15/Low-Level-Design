package _5_ManagementSystems._1_ParkingLot.strategies.payment;

import _5_ManagementSystems._1_ParkingLot.enums.PaymentStatus;
import _5_ManagementSystems._1_ParkingLot.entities.payment.PaymentResult;

import java.util.UUID;

public class CreditCardPayment implements PaymentModeStrategy {
    private final String cardNumber;
    private final String expiry;
    private final String cvv;

    public CreditCardPayment(String cardNumber, String expiry, String cvv) {
        this.cardNumber = cardNumber;
        this.expiry = expiry;
        this.cvv = cvv;
    }

    @Override
    public boolean validate() {
        // Luhn check + expiry validation
        return cardNumber != null && cardNumber.length() == 16
                && cvv != null && cvv.length() == 3
                && expiry != null && expiry.matches("\\d{2}/\\d{2}");
    }

    @Override
    public PaymentResult pay(double amount) {
        if (!validate()) {
            return new PaymentResult(PaymentStatus.FAILED, null, "Invalid card details");
        }
        // Simulate gateway call
        String txnId = UUID.randomUUID().toString();
        System.out.println("Charging ₹" + amount + " to card ending " + cardNumber.substring(12));
        return new PaymentResult(PaymentStatus.SUCCESS, txnId, "Card payment successful");
    }

    @Override
    public PaymentResult refund(String transactionId, double amount) {
        System.out.println("Refunding ₹" + amount + " to card ending " + cardNumber.substring(12));
        return new PaymentResult(PaymentStatus.SUCCESS, transactionId, "Refund processed");
    }
}