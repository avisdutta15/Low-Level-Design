package _5_ManagementSystems._3_InventoryManagementSystem.observer;

import _5_ManagementSystems._3_InventoryManagementSystem.entities.InventoryManagementSystem;
import _5_ManagementSystems._3_InventoryManagementSystem.entities.Product;
import _5_ManagementSystems._3_InventoryManagementSystem.enums.InventoryEvent;

public class LowStockAlertObserver implements InventoryObserver {
    private final InventoryManagementSystem system;

    public LowStockAlertObserver(InventoryManagementSystem system) {
        this.system = system;
    }

    @Override
    public void onEvent(InventoryEventData data) {
        if (data.getEvent() != InventoryEvent.STOCK_REMOVED &&
            data.getEvent() != InventoryEvent.STOCK_TRANSFERRED) return;

        Product product = system.getCatalog().get(data.getProductId());
        if (product == null) return;

        if (data.getCurrentStock() < product.getLowStockThreshold()) {
            System.out.println("[LOW STOCK ALERT] product=" + product.getName() +
                    " | warehouse=" + data.getWarehouseId() +
                    " | stock=" + data.getCurrentStock() +
                    " | threshold=" + product.getLowStockThreshold());
        }
    }
}
