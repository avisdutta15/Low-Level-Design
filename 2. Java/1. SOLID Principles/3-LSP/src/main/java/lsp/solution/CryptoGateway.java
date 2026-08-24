package lsp.solution;

/**
 * LSP Solution: CryptoGateway does NOT implement Refundable.
 * It only extends PaymentGateway — no broken contract.
 */
public class CryptoGateway extends PaymentGateway {

    @Override
    public String charge(String customerId, double amount, String paymentDetails) {
        String txnId = "CRYPTO-" + System.currentTimeMillis();
        System.out.println("[CRYPTO] Charged wallet " + paymentDetails
                + " for $" + amount + " | Txn: " + txnId);
        return txnId;
    }
}
