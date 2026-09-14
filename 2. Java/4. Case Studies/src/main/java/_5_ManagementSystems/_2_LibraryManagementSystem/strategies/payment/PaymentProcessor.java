package _5_ManagementSystems._2_LibraryManagementSystem.strategies.payment;

import _5_ManagementSystems._2_LibraryManagementSystem.entities.payment.PaymentResult;
import _5_ManagementSystems._2_LibraryManagementSystem.enums.PaymentStatus;

import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

public class PaymentProcessor {
    private final Set<String> processedRequests = ConcurrentHashMap.newKeySet();

    public PaymentResult processPayment(PaymentModeStrategy strategy, String requestId, double amount){
        // Idempotency
        if (processedRequests.contains(requestId)) {
            return new PaymentResult(PaymentStatus.FAILED, null, "Duplicate request: " + requestId);
        }

        // Validate
        if (!strategy.validate()) {
            return new PaymentResult(PaymentStatus.FAILED, null, "Payment validation failed");
        }

        // Process
        PaymentResult result = strategy.pay(amount);

        // Track successful payments
        if (result.getStatus() == PaymentStatus.SUCCESS) {
            processedRequests.add(requestId);
        }

        return result;
    }

    public PaymentResult processRefund(PaymentModeStrategy strategy, String transactionId, double amount) {
        return strategy.refund(transactionId, amount);
    }
}
