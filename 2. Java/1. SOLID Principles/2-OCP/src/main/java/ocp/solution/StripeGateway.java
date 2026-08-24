package ocp.solution;

public class StripeGateway implements PaymentGateway {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        String txnId = "STRIPE-" + System.currentTimeMillis();
        System.out.println("[STRIPE] Charged card " + paymentDetails.substring(0, 4)
                + "**** for $" + amount + " | Txn: " + txnId);
        return txnId;
    }
}
