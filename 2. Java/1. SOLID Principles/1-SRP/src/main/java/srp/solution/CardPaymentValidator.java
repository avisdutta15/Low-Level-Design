package srp.solution;

public class CardPaymentValidator implements PaymentValidator {

    @Override
    public boolean validate(double amount, String cardNumber) {
        if (amount <= 0) {
            System.out.println("[VALIDATOR] Amount must be positive.");
            return false;
        }
        if (cardNumber == null || cardNumber.length() < 13) {
            System.out.println("[VALIDATOR] Invalid card number.");
            return false;
        }
        System.out.println("[VALIDATOR] Payment input is valid.");
        return true;
    }
}
