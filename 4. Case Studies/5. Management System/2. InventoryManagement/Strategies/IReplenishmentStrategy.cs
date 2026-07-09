namespace InventoryManagementSystem.Strategies;

/// <summary>
/// Defines a strategy for replenishing stock when it falls below threshold.
/// </summary>
public interface IReplenishmentStrategy
{
    /// <summary>
    /// Calculates how many units to replenish given the current stock level.
    /// </summary>
    int CalculateReplenishmentQuantity(int currentStock, int threshold);
}
