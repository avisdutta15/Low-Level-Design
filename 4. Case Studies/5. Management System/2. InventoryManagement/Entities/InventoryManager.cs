using System.Collections.Concurrent;
using InventoryManagementSystem.Observers;
using InventoryManagementSystem.Entities.Products;

namespace InventoryManagementSystem.Entities;

public class InventoryManager
{
    private readonly ConcurrentDictionary<string, Warehouse> _warehouses = new();
    private readonly List<IAlertListener> _observers = new();
    private readonly List<StockMovement> _movements = new();
    private readonly List<AlertConfig> _alertConfigs = new();
    private readonly object _observerLock = new();
    private readonly object _movementLock = new();

    public IReadOnlyList<StockMovement> Movements
    {
        get { lock (_movementLock) { return _movements.ToList().AsReadOnly(); } }
    }

    // --- Warehouse Management ---

    public void AddWarehouse(Warehouse warehouse)
    {
        ArgumentNullException.ThrowIfNull(warehouse);
        if (!_warehouses.TryAdd(warehouse.Id, warehouse))
            throw new InvalidOperationException($"Warehouse '{warehouse.Id}' already exists.");
    }

    public void RemoveWarehouse(string warehouseId)
    {
        _warehouses.TryRemove(warehouseId, out _);
    }

    public Warehouse? GetWarehouse(string warehouseId)
    {
        _warehouses.TryGetValue(warehouseId, out Warehouse? warehouse);
        return warehouse;
    }

    // --- Stock Operations ---

    public void AddStock(string warehouseId, Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);
        var warehouse = GetWarehouse(warehouseId)
            ?? throw new InvalidOperationException($"Warehouse '{warehouseId}' not found.");

        warehouse.AddStock(product, quantity);
        RecordMovement(new StockMovement(product.Sku, quantity, warehouseId, null, MovementType.Addition));
    }

    public void RemoveStock(string warehouseId, string sku, int quantity)
    {
        var warehouse = GetWarehouse(warehouseId)
            ?? throw new InvalidOperationException($"Warehouse '{warehouseId}' not found.");

        bool success = warehouse.RemoveStock(sku, quantity);
        if (!success)
            throw new InvalidOperationException($"Cannot remove {quantity} of '{sku}' from warehouse '{warehouseId}'. Insufficient stock or product not found.");

        RecordMovement(new StockMovement(sku, quantity, warehouseId, null, MovementType.Removal));

        // Since we removed stock, check the alert config, replenish and notify
        CheckAndNotifyLowStock(warehouseId, sku);
    }

    /// <summary>
    /// Returns warehouse IDs that can fulfill the requested quantity for the given SKU.
    /// Note: This is a point-in-time snapshot — not a reservation.
    /// </summary>
    public List<string> CheckAvailability(string sku, int quantity)
    {
        var result = new List<string>();
        foreach (var kvp in _warehouses)
        {
            if (kvp.Value.GetStockLevel(sku) >= quantity)
                result.Add(kvp.Key);
        }
        return result;
    }

    /// <summary>
    /// Atomically transfers stock between two warehouses using ordered locking to prevent deadlocks.
    /// </summary>
    public void TransferStock(string sourceWarehouseId, string destinationWarehouseId, string sku, int quantity)
    {
        // Validate Parameters: Quantity
        if (quantity <= 0)
            throw new ArgumentException("Transfer quantity must be positive.", nameof(quantity));

        // Validate Parameters: Source and Destination Warehouses
        if (sourceWarehouseId == destinationWarehouseId)
            throw new ArgumentException("Source and destination warehouses must be different.");
    
        var source = GetWarehouse(sourceWarehouseId)
            ?? throw new InvalidOperationException($"Source Warehouse '{sourceWarehouseId}' not found.");
        var destination = GetWarehouse(destinationWarehouseId)
            ?? throw new InvalidOperationException($"Destination warehouse '{destinationWarehouseId}' not found.");

        // Get the product
        var product = source.GetProduct(sku)
            ?? throw new InvalidOperationException($"Product '{sku}' not found in source warehouse '{sourceWarehouseId}'.");

        // Removal and Addition should be atomic. Ordered locking by warehouse ID to prevent deadlocks
        Warehouse firstLock = string.CompareOrdinal(sourceWarehouseId, destinationWarehouseId) < 0 ? source : destination;
        Warehouse secondLock = firstLock == source ? destination : source;

        lock (firstLock) 
        {
            lock (secondLock)
            {
                bool removed = source.RemoveStock(sku, quantity);
                if (!removed)
                    throw new InvalidOperationException($"Cannot transfer {quantity} of '{sku}' from warehouse '{sourceWarehouseId}'. Insufficient stock.");

                destination.AddStock(product, quantity);
            }
        }
        

        RecordMovement(new StockMovement(sku, quantity, sourceWarehouseId, destinationWarehouseId, MovementType.TransferOut));
        RecordMovement(new StockMovement(sku, quantity, destinationWarehouseId, sourceWarehouseId, MovementType.TransferIn));

        // Since we removed stock from source, check the alert config, replenish and notify
        CheckAndNotifyLowStock(sourceWarehouseId, sku);
    }

    // --- Observer / Alert Management ---

    public void Subscribe(IAlertListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (_observerLock)
        {
            if (!_observers.Contains(listener))
                _observers.Add(listener);
        }
    }

    public void Unsubscribe(IAlertListener listener)
    {
        lock (_observerLock) { _observers.Remove(listener); }
    }

    public void AddAlertConfig(AlertConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        lock (_observerLock) { _alertConfigs.Add(config); }
    }

    private void CheckAndNotifyLowStock(string warehouseId, string sku)
    {
        // Validate warehouse exists
        var warehouse = GetWarehouse(warehouseId);
        if (warehouse == null) return;

        // Check all alert configs for this warehouse + sku combination
        int currentQty = warehouse.GetStockLevel(sku);

        List<AlertConfig> matchingConfigs;
        lock (_observerLock)
        {
            matchingConfigs = _alertConfigs
                .Where(c => c.ProductSku == sku && c.WarehouseId == warehouseId)
                .ToList();
        }

        foreach (var config in matchingConfigs)
        {
            // if the current quantity is below threshold, trigger alert and replenishment
            if (currentQty <= config.Threshold)
            {
                NotifySubscribers(warehouseId, sku, currentQty, config.Threshold);
                TriggerReplenishment(warehouse, sku, currentQty, config);
            }
        }
    }

    private void TriggerReplenishment(Warehouse warehouse, string sku, int currentQty, AlertConfig config)
    {
        if (config.ReplenishmentStrategy == null)
            return;

        int replenishQty = config.ReplenishmentStrategy.CalculateReplenishmentQuantity(currentQty, config.Threshold);
        if (replenishQty <= 0)
            return;

        var product = warehouse.GetProduct(sku);
        if (product == null)
            return;

        warehouse.AddStock(product, replenishQty);
        RecordMovement(new StockMovement(sku, replenishQty, warehouse.Id, null, MovementType.Addition));
        Console.WriteLine($"[REPLENISHMENT] Warehouse: {warehouse.Id}, SKU: {sku}, Replenished: {replenishQty} units");
    }

    private void NotifySubscribers(string warehouseId, string sku, int currentQuantity, int threshold)
    {
        List<IAlertListener> snapshot = new();
        lock (_observerLock) { 
            snapshot = _observers.ToList(); 
        }

        foreach (var observer in snapshot)
        {
            observer.OnLowStock(warehouseId, sku, currentQuantity, threshold);
        }
    }

    // --- Private Helpers ---
    private void RecordMovement(StockMovement movement)
    {
        lock (_movementLock) { _movements.Add(movement); }
    }
}
