package lsp.solution;

/**
 * LSP Solution: PaymentGateway only defines charge().
 * Refund capability is separated into a distinct interface.
 */
public abstract class PaymentGateway {

    public abstract String charge(String customerId, double amount, String paymentDetails);
}
