package realworld;

public class EmailOrderNotifier implements OrderNotifier {
    @Override
    public void sendConfirmation(Order order) {
        System.out.println("Sending confirmation email for order " + order.getOrderId());
    }
}
