package realworld;

// OCP: New discount? Create a new class. Don't touch existing ones.
public class BlackFridayDiscount implements DiscountPolicy {
    @Override
    public double calculateDiscount(Order order) {
        return order.getTotalAmount() * 0.30; // 30% off
    }
}
