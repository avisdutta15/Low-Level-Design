package _5_ManagementSystems._3_InventoryManagementSystem.entities;

import _5_ManagementSystems._3_InventoryManagementSystem.strategies.ReplenishmentStrategy;
import _5_ManagementSystems._3_InventoryManagementSystem.enums.InventoryEvent;
import _5_ManagementSystems._3_InventoryManagementSystem.enums.MovementType;
import _5_ManagementSystems._3_InventoryManagementSystem.observer.InventoryEventData;
import _5_ManagementSystems._3_InventoryManagementSystem.observer.InventoryObserver;

import java.util.*;
import java.util.concurrent.*;

public class InventoryManagementSystem {
    private final ConcurrentHashMap<String, Warehouse> warehouses;
    private final ConcurrentHashMap<String, Product> catalog;
    private final CopyOnWriteArrayList<StockMovement> auditLog;
    private final CopyOnWriteArrayList<InventoryObserver> observers;
    private final ReplenishmentStrategy replenishmentStrategy;

    public InventoryManagementSystem(ReplenishmentStrategy replenishmentStrategy) {
        this.warehouses = new ConcurrentHashMap<>();
        this.catalog = new ConcurrentHashMap<>();
        this.auditLog = new CopyOnWriteArrayList<>();
        this.observers = new CopyOnWriteArrayList<>();
        this.replenishmentStrategy = replenishmentStrategy;
    }

    // ---- Setup ----
    public void addWarehouse(Warehouse warehouse) { warehouses.put(warehouse.getId(), warehouse); }
    public void addProduct(Product product) { catalog.put(product.getSku(), product); }
    public Map<String, Product> getCatalog() { return this.catalog; }
    public List<StockMovement> getAuditLog() { return Collections.unmodifiableList(auditLog); }

    // ---- Observer ----
    public void subscribe(InventoryObserver observer) { observers.add(observer); }
    public void unsubscribe(InventoryObserver observer) { observers.remove(observer); }
    private void notifyObservers(InventoryEventData data) {
        for (InventoryObserver observer : observers) {
            observer.onEvent(data);
        }
    }

    // ---- Add Stock (receive shipment) ----
    public void addStock(String warehouseId, String productId, int quantity) {
        // validate inputs
        Warehouse warehouse = warehouses.get(warehouseId);
        Product product = catalog.get(productId);
        if (warehouse == null) throw new RuntimeException("Invalid Warehouse: " + warehouseId);
        if (product == null) throw new RuntimeException("Invalid Product SKU: " + productId);

        warehouse.addStock(productId, quantity);    // add stock to warehouse

        int currentStock = warehouse.getCurrentStock(productId);
        auditLog.add(new StockMovement(MovementType.ADD, warehouseId, productId, quantity));
        notifyObservers(new InventoryEventData(InventoryEvent.STOCK_ADDED, warehouseId, productId, quantity, currentStock));
    }

    // ---- Remove Stock (fulfill order) ----
    public boolean removeStock(String warehouseId, String productId, int quantity) {
        Warehouse warehouse = warehouses.get(warehouseId);
        Product product = catalog.get(productId);
        if (warehouse == null) throw new RuntimeException("Invalid Warehouse: " + warehouseId);
        if (product == null) throw new RuntimeException("Invalid Product SKU: " + productId);

        boolean success = warehouse.removeStock(productId, quantity);   // remove the stock if product exists
        if (!success) return false;

        int currentStock = warehouse.getCurrentStock(productId);
        auditLog.add(new StockMovement(MovementType.REMOVE, warehouseId, productId, quantity));
        notifyObservers(new InventoryEventData(InventoryEvent.STOCK_REMOVED, warehouseId, productId, quantity, currentStock));

        triggerReplenishmentIfNeeded(warehouse, warehouseId, product, currentStock);
        return true;
    }

    // ---- Transfer Stock (ordered locking — deadlock-free) ----
    public void transferStock(String fromId, String toId, String productId, int quantity) {
        Warehouse from = warehouses.get(fromId);
        Warehouse to = warehouses.get(toId);
        if (from == null) throw new RuntimeException("Warehouse not found: " + fromId);
        if (to == null) throw new RuntimeException("Warehouse not found: " + toId);

        // Order locks by warehouse ID to prevent deadlock
        Warehouse first, second;
        if (fromId.compareTo(toId) < 0) {
            first = from; second = to;
        } else {
            first = to; second = from;
        }

        synchronized (first) {
            synchronized (second) {
                boolean removed = from.removeStock(productId, quantity);
                if (!removed) throw new RuntimeException("Cannot transfer stock");
                to.addStock(productId, quantity);

                int sourceStock = from.getCurrentStock(productId);
                auditLog.add(new StockMovement(MovementType.TRANSFER_OUT, fromId, productId, quantity));
                auditLog.add(new StockMovement(MovementType.TRANSFER_IN, toId, productId, quantity));
                notifyObservers(new InventoryEventData(InventoryEvent.STOCK_TRANSFERRED, fromId, productId, quantity, sourceStock));

                triggerReplenishmentIfNeeded(from, fromId, catalog.get(productId), sourceStock); // trigger replenishment from 'from' warehouse.
            }
        }
    }

    // ---- Replenishment ----
    private void triggerReplenishmentIfNeeded(Warehouse warehouse, String warehouseId, Product product, int currentStock) {
        if (product == null || currentStock >= product.getLowStockThreshold()) return;  // return if no replenishment needed or product is null
        int reorderQty = replenishmentStrategy.calculateReorderQuantity(product);   // calculate the quantity to replenish

        warehouse.addStock(product.getSku(), reorderQty);

        auditLog.add(new StockMovement(MovementType.ADD, warehouseId, product.getSku(), reorderQty));
        notifyObservers(new InventoryEventData(InventoryEvent.STOCK_REPLENISHED, warehouseId, product.getSku(), reorderQty, warehouse.getCurrentStock(product.getSku())));
    }
}
