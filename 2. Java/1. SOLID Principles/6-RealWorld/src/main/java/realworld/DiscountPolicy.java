package realworld;

// OCP: An interface that allows us to add endless discount rules
public interface DiscountPolicy {
    double calculateDiscount(Order order);
}
