package srp.solution;

/**
 * Single responsibility: charge the customer's card.
 */
public interface PaymentGateway {
    String charge(String customerId, double amount, String cardNumber);
}
