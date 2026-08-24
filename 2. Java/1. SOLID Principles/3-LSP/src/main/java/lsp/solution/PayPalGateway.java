package lsp.solution;

public class PayPalGateway extends PaymentGateway implements Refundable {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        String txnId = "PAYPAL-" + System.currentTimeMillis();
        System.out.println("[PAYPAL] Charged account " + paymentDetails
                + " for $" + amount + " | Txn: " + txnId);
        return txnId;
    }

    @Override
    public void refund(String txnId, double amount) {
        System.out.println("[PAYPAL] Refunded $" + amount + " for transaction " + txnId);
    }
}
