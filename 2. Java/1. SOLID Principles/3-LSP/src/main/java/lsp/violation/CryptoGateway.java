package lsp.violation;

/**
 * LSP Violation: CryptoGateway cannot support refund(),
 * so it throws an exception — breaking the Liskov Substitution Principle.
 */
public class CryptoGateway extends PaymentGateway {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        String txnId = "CRYPTO-" + System.currentTimeMillis();
        System.out.println("[CRYPTO] Charged wallet " + paymentDetails
                + " for $" + amount + " | Txn: " + txnId);
        return txnId;
    }

    @Override
    public void refund(String txnId, double amount) {
        throw new UnsupportedOperationException(
                "Crypto payments are irreversible and cannot be refunded!");
    }
}
