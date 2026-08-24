package ocp.solution;

public class PayPalGateway implements PaymentGateway {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        String txnId = "PAYPAL-" + System.currentTimeMillis();
        System.out.println("[PAYPAL] Charged account " + paymentDetails
                + " for $" + amount + " | Txn: " + txnId);
        return txnId;
    }
}
