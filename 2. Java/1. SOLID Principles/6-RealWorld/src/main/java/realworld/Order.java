package realworld;

public class Order {
    private String orderId;
    private double totalAmount;

    public Order(String orderId, double totalAmount) {
        this.orderId = orderId;
        this.totalAmount = totalAmount;
    }

    public String getOrderId() {
        return orderId;
    }

    public double getTotalAmount() {
        return totalAmount;
    }

    public void applyDiscount(double discount) {
        this.totalAmount -= discount;
        System.out.println("Discount of $" + discount + " applied. New total: $" + totalAmount);
    }
}
