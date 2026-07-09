using InventoryManagementSystem.Strategies;

namespace InventoryManagementSystem.Entities;

/// <summary>
/// Defines a low-stock threshold for a specific product in a specific warehouse,
/// with an optional replenishment strategy.
/// </summary>
public class AlertConfig
{
    public string WarehouseId { get; }
    public string ProductSku { get; }
    public int Threshold { get; }
    public IReplenishmentStrategy? ReplenishmentStrategy { get; }

    public AlertConfig(string warehouseId, string productSku, int threshold, IReplenishmentStrategy? replenishmentStrategy = null)
    {
        if (threshold < 0)
            throw new ArgumentException("Threshold must be non-negative.", nameof(threshold));

        WarehouseId = warehouseId;
        ProductSku = productSku;
        Threshold = threshold;
        ReplenishmentStrategy = replenishmentStrategy;
    }
}
