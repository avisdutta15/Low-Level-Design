package lsp.violation;

/**
 * LSP VIOLATION: This abstract class forces ALL subclasses to implement refund(),
 * but not all payment gateways support refunds (e.g., Crypto).
 */
public abstract class PaymentGateway {

    public abstract String charge(String customerId, double amount, String paymentDetails);

    public abstract void refund(String txnId, double amount);
}
