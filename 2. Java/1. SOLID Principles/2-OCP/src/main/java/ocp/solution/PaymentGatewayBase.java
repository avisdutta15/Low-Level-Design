package ocp.solution;

/**
 * Abstract base that adds shared behavior (e.g., fraud checks)
 * while still being open for extension via subclasses.
 */
public abstract class PaymentGatewayBase implements PaymentGateway {

    protected boolean performFraudCheck(String customerId, double amount) {
        if (amount > 10000) {
            System.out.println("[FRAUD CHECK] High-value transaction flagged for customer " + customerId);
            return false;
        }
        System.out.println("[FRAUD CHECK] Passed for customer " + customerId);
        return true;
    }
}
