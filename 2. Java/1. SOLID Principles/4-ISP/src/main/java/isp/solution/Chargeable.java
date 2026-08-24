package isp.solution;

public interface Chargeable {
    String charge(String customerId, double amount, String paymentDetails);
}
