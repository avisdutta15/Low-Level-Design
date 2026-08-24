package srp.solution;

/**
 * Single responsibility: validate payment input.
 */
public interface PaymentValidator {
    boolean validate(double amount, String cardNumber);
}
