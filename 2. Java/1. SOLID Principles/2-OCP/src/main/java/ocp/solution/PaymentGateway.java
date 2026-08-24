package ocp.solution;

/**
 * OCP Solution: Open for extension via new implementations,
 * closed for modification — no if-else chains.
 */
public interface PaymentGateway {
    String charge(String customerId, double amount, String paymentDetails);
}
