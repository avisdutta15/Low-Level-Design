package realworld;

public class SqlInventoryManager implements InventoryManager {
    @Override
    public void reserveStock(Order order) {
        System.out.println("Reserving stock for order " + order.getOrderId() + " in database...");
    }
}
