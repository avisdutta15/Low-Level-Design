package _5_ManagementSystems._3_InventoryManagementSystem;

/*
* Domain Notes:
*   An InventoryManagementSystem manges multiple Warehouse.
*   A Warehouse stores Product with stock quantities.
*   A Product is a catalog entry (id, name, category) - stock is tracked per warehouse and not on the product.
*   A StockEntry is the per warehouse stock for a product (maneged via AtomicInteger + threshold)
*   A StockMovement is an audit record of every add/remove/transfer with timestamp.
*   Transfers move stock between two warehouses atomically (ordered locking to prevent deadlock)
*   Low-stock alerts fire asynchronously when stock drops below threshold — observer pattern.
*   Replenishment strategy decides how much to reorder — strategy pattern.
* */

import _5_ManagementSystems._3_InventoryManagementSystem.entities.*;
import _5_ManagementSystems._3_InventoryManagementSystem.observer.*;
import _5_ManagementSystems._3_InventoryManagementSystem.strategies.FixedAmountReplenishment;

import java.util.concurrent.*;

public class Main {
    public static void main(String[] args) throws InterruptedException {
        InventoryManagementSystem system = new InventoryManagementSystem(new FixedAmountReplenishment(50));

        // Subscribe observers
        AuditLogObserver auditObserver = new AuditLogObserver();
        LowStockAlertObserver lowStockObserver = new LowStockAlertObserver(system);
        AsyncInventoryObserver asyncLowStock = new AsyncInventoryObserver(lowStockObserver);
        system.subscribe(auditObserver);
        system.subscribe(asyncLowStock);

        testAddAndRemoveStock(system);
        testRemoveMoreThanAvailable(system);
        testTransferStock(system);
        testLowStockReplenishment(system);
        testConcurrentRemoves(system);
        testAuditLog(system);

        Thread.sleep(500); // let async observers finish
        asyncLowStock.shutdown();
    }

    private static void testAddAndRemoveStock(InventoryManagementSystem system) {
        System.out.println("\n--- Add & Remove Stock ---");
        system.addWarehouse(new Warehouse("WH-1", "Mumbai Warehouse", "Mumbai"));
        system.addWarehouse(new Warehouse("WH-2", "Delhi Warehouse", "Delhi"));

        system.addProduct(new Product("SKU-001", "Laptop", "Electronics", 10));
        system.addProduct(new Product("SKU-002", "Mouse", "Accessories", 20));

        system.addStock("WH-1", "SKU-001", 100);
        system.addStock("WH-1", "SKU-002", 200);
        system.addStock("WH-2", "SKU-001", 50);

        boolean removed = system.removeStock("WH-1", "SKU-001", 30);
        System.out.println("Removed 30 laptops from WH-1: " + removed);
    }

    private static void testRemoveMoreThanAvailable(InventoryManagementSystem system) {
        System.out.println("\n--- Remove More Than Available ---");
        boolean result = system.removeStock("WH-2", "SKU-001", 999);
        System.out.println("Tried removing 999 from WH-2 (has 50): " + result); // false
    }

    private static void testTransferStock(InventoryManagementSystem system) {
        System.out.println("\n--- Transfer Stock ---");
        system.transferStock("WH-1", "WH-2", "SKU-002", 80);
        System.out.println("Transferred 80 mice from WH-1 to WH-2");
    }

    private static void testLowStockReplenishment(InventoryManagementSystem system) throws InterruptedException {
        System.out.println("\n--- Low Stock + Auto Replenishment ---");
        // SKU-001 has threshold=10, WH-2 has 50. Remove 45 to drop below threshold.
        system.removeStock("WH-2", "SKU-001", 45);
        System.out.println("Removed 45 laptops from WH-2 — should trigger low stock alert + replenishment");
        Thread.sleep(300); // let async observer print
    }

    private static void testConcurrentRemoves(InventoryManagementSystem system) throws InterruptedException {
        System.out.println("\n--- Concurrent Removes (10 threads, limited stock) ---");
        system.addProduct(new Product("SKU-003", "Keyboard", "Accessories", 5));
        system.addStock("WH-1", "SKU-003", 15);

        // 10 threads each try to remove 5 — only 3 should succeed (15 / 5 = 3)
        ExecutorService executor = Executors.newFixedThreadPool(10);
        for (int i = 0; i < 10; i++) {
            int threadNum = i + 1;
            executor.submit(() -> {
                boolean success = system.removeStock("WH-1", "SKU-003", 5);
                System.out.println("[Thread-" + threadNum + "] remove 5 keyboards: " + (success ? "SUCCESS" : "FAILED"));
            });
        }
        executor.shutdown();
        executor.awaitTermination(5, TimeUnit.SECONDS);
        System.out.println("Expected: exactly 3 successes out of 10");
    }

    private static void testAuditLog(InventoryManagementSystem system) {
        System.out.println("\n--- Audit Log ---");
        System.out.println("Total movements recorded: " + system.getAuditLog().size());
        system.getAuditLog().forEach(m ->
                System.out.println("  " + m.getType() + " | " + m.getWarehouseId() +
                        " | " + m.getProductId() + " | qty=" + m.getQuantity()));
    }
}
