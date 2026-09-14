package _5_ManagementSystems._3_InventoryManagementSystem.entities;

import java.util.concurrent.atomic.AtomicInteger;

public class StockEntry {
    private final String productId;
    private final AtomicInteger currentStock;

    public StockEntry(String productId) {
        this.productId = productId;
        this.currentStock = new AtomicInteger(0);
    }

    public void addStock(int quantity){
        currentStock.addAndGet(quantity);
    }

    public boolean removeQuantity(int quantity){
        while(true){
            int current = currentStock.get();
            if(current < quantity) return false;
            if(currentStock.compareAndSet(current, current - quantity)) return true;
        }
    }

    public String getProductId() {
        return productId;
    }

    public int getCurrentStock() {
        return currentStock.get();
    }

}
