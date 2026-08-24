package lsp.violation;

public class StripeGateway extends PaymentGateway {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        String txnId = "STRIPE-" + System.currentTimeMillis();
        System.out.println("[STRIPE] Charged card " + paymentDetails.substring(0, 4)
                + "**** for $" + amount + " | Txn: " + txnId);
        return txnId;
    }

    @Override
    public void refund(String txnId, double amount) {
        System.out.println("[STRIPE] Refunded $" + amount + " for transaction " + txnId);
    }
}
