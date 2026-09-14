package _5_ManagementSystems._3_InventoryManagementSystem.entities;

import _5_ManagementSystems._3_InventoryManagementSystem.enums.MovementType;

import java.time.LocalDateTime;
import java.util.UUID;

public class StockMovement {
    private final String movementId;
    private final MovementType type;
    private final String warehouseId;
    private final String productId;
    private final int quantity;
    private final LocalDateTime timestamp;

    public StockMovement(MovementType type, String warehouseId, String productId, int quantity) {
        this.movementId = UUID.randomUUID().toString();
        this.type = type;
        this.warehouseId = warehouseId;
        this.productId = productId;
        this.quantity = quantity;
        this.timestamp = LocalDateTime.now();
    }

    public String getMovementId() { return movementId; }
    public MovementType getType() { return type; }
    public String getWarehouseId() { return warehouseId; }
    public String getProductId() { return productId; }
    public int getQuantity() { return quantity; }
    public LocalDateTime getTimestamp() { return timestamp; }
}
