package ocp.solution;

public class UpiGateway implements PaymentGateway {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        String txnId = "UPI-" + System.currentTimeMillis();
        System.out.println("[UPI] Charged UPI ID " + paymentDetails
                + " for $" + amount + " | Txn: " + txnId);
        return txnId;
    }
}
