package dip.solution;

/**
 * DIP Solution: Abstraction for charging payments.
 * Both high-level and low-level modules depend on this interface.
 */
public interface Chargeable {
    String charge(String customerId, double amount, String paymentDetails);
}
