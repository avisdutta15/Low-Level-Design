package dip.solution;

/**
 * DIP Solution: Abstraction for refunding payments.
 */
public interface Refundable {
    void refund(String txnId, double amount);
}
