package _5_ManagementSystems._3_InventoryManagementSystem.observer;

import _5_ManagementSystems._3_InventoryManagementSystem.enums.InventoryEvent;

public class InventoryEventData {
    private final InventoryEvent event;
    private final String warehouseId;
    private final String productId;
    private final int quantity;
    private final int currentStock;

    public InventoryEventData(InventoryEvent event, String warehouseId, String productId, int quantity, int currentStock) {
        this.event = event;
        this.warehouseId = warehouseId;
        this.productId = productId;
        this.quantity = quantity;
        this.currentStock = currentStock;
    }

    public InventoryEvent getEvent() { return event; }
    public String getWarehouseId() { return warehouseId; }
    public String getProductId() { return productId; }
    public int getQuantity() { return quantity; }
    public int getCurrentStock() { return currentStock; }
}
