package _5_ManagementSystems._3_InventoryManagementSystem.observer;

public class AuditLogObserver implements InventoryObserver {
    @Override
    public void onEvent(InventoryEventData data) {
        System.out.println("[AUDIT] " + data.getEvent() + " | warehouse=" + data.getWarehouseId() +
                " | product=" + data.getProductId() + " | qty=" + data.getQuantity() +
                " | stock=" + data.getCurrentStock());
    }
}
