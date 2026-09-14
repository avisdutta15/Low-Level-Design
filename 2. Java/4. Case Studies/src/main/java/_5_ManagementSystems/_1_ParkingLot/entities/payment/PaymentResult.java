package _5_ManagementSystems._1_ParkingLot.entities.payment;

import _5_ManagementSystems._1_ParkingLot.enums.PaymentStatus;

public class PaymentResult {
    private final PaymentStatus status;
    private final String transactionId;
    private final String message;

    public PaymentResult(PaymentStatus status, String transactionId, String message) {
        this.status = status;
        this.transactionId = transactionId;
        this.message = message;
    }

    public PaymentStatus getStatus() { return status; }
    public String getTransactionId() { return transactionId; }
    public String getMessage() { return message; }

    @Override
    public String toString() {
        return "PaymentResult{status=" + status + ", txnId='" + transactionId + "', message='" + message + "'}";
    }
}
