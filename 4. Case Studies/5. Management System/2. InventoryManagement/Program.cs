using InventoryManagementSystem.Entities;
using InventoryManagementSystem.Entities.Products;
using InventoryManagementSystem.Enums;
using InventoryManagementSystem.Observers;
using InventoryManagementSystem.Strategies;

// Core Entities:
// InventoryManager - This has a list of warehouses
//           - Dictionary<string, Warehouse> warehouses
//           - List<Observer> observers
//           - GetWarehouse(warehouseId)
//           - AddWarehouse(warehouse)
//           - Subscribe()
//           - Unsubscribe()
//           - NotifySubscribers()
// Warehouse - This has a list of items and their counts
//           - Id
//           - Name
//           - Location
//           - Dictionary<string, Product> products
//           - AddProduct(Product, quantity)
//           - RemoveProduct(sku, quantity)
//           - GetProductQuantity(sku)
// Product - This is a simple class with Id, Name, etc.
// StockMovement - This records every stock movement with timestamp, product, quantity, source/destination warehouse
// AlertConfig
// AlertListener

InventoryManager inventoryManager = new InventoryManager();

// Add Warehouses
inventoryManager.AddWarehouse(new Warehouse("WH001", "Main Warehouse", "New York"));
inventoryManager.AddWarehouse(new Warehouse("WH002", "Secondary Warehouse", "Los Angeles"));

// Subscribe a console alert listener
var alertListener = new ConsoleAlertListener();
inventoryManager.Subscribe(alertListener);

// Configure low-stock alert: warn when S25 drops to 5 or below in WH001, and auto-replenish to 100
inventoryManager.AddAlertConfig(new AlertConfig("WH001", "S25-128GB-Black", 5, new ReplenishToMaxStrategy(100)));

// Add stock
var samsung = new SamsungS25("S25-128GB-Black", "Samsung Galaxy S25 128GB Black", 799.99, ProductCategory.ELECTRONICS);
inventoryManager.AddStock("WH001", samsung, 100);
inventoryManager.AddStock("WH002", samsung, 50);

// Remove stock (fulfilling an order)
inventoryManager.RemoveStock("WH001", "S25-128GB-Black", 10);

// Check availability
var available = inventoryManager.CheckAvailability("S25-128GB-Black", 60);
Console.WriteLine($"Warehouses that can fulfill 60 units: {string.Join(", ", available)}");

// Transfer stock
inventoryManager.TransferStock("WH001", "WH002", "S25-128GB-Black", 20);

// Print audit log
Console.WriteLine("\n--- Stock Movement Audit Log ---");
foreach (var movement in inventoryManager.Movements)
{
    Console.WriteLine(movement);
}
