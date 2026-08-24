package realworld;

/**
 * SRP: Its single responsibility is orchestration.
 * DIP: It depends only on abstractions (interfaces), not concrete classes.
 *
 * This class NEVER needs to be modified when we add new payment methods,
 * discount policies, or notification channels.
 */
public class OrderProcessor {

    private final PaymentProcessor paymentProcessor;
    private final InventoryManager inventoryManager;
    private final OrderNotifier notifier;
    private final DiscountPolicy discountPolicy;

    public OrderProcessor(
            PaymentProcessor paymentProcessor,
            InventoryManager inventoryManager,
            OrderNotifier notifier,
            DiscountPolicy discountPolicy) {
        this.paymentProcessor = paymentProcessor;
        this.inventoryManager = inventoryManager;
        this.notifier = notifier;
        this.discountPolicy = discountPolicy;
    }

    public void process(Order order, PaymentDetails payment) {
        // 1. Apply Discounts (OCP in action)
        double discount = discountPolicy.calculateDiscount(order);
        order.applyDiscount(discount);

        // 2. Process Payment (LSP in action - doesn't care if it's CC or PayPal)
        if (paymentProcessor.processPayment(order, payment)) {
            // 3. Update Inventory
            inventoryManager.reserveStock(order);

            // 4. Notify User
            notifier.sendConfirmation(order);
        }
    }

    public static void main(String[] args) {
        // Wire up dependencies (in real app, Spring does this automatically)
        OrderProcessor processor = new OrderProcessor(
                new CreditCardProcessor(),
                new SqlInventoryManager(),
                new EmailOrderNotifier(),
                new BlackFridayDiscount()
        );

        Order order = new Order("ORD-001", 200.00);
        PaymentDetails payment = new PaymentDetails("4111-1111-1111-1111", "CREDIT_CARD");

        processor.process(order, payment);
    }
}
