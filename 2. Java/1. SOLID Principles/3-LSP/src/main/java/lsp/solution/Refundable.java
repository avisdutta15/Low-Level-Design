package lsp.solution;

/**
 * Separate interface for gateways that support refunds.
 */
public interface Refundable {
    void refund(String txnId, double amount);
}
