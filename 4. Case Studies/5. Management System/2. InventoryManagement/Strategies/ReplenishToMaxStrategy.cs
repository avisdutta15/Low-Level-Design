namespace InventoryManagementSystem.Strategies;

/// <summary>
/// Replenishes stock up to a target maximum level.
/// </summary>
public class ReplenishToMaxStrategy : IReplenishmentStrategy
{
    private readonly int _maxLevel;

    public ReplenishToMaxStrategy(int maxLevel)
    {
        if (maxLevel <= 0)
            throw new ArgumentException("Max level must be positive.", nameof(maxLevel));
        _maxLevel = maxLevel;
    }

    public int CalculateReplenishmentQuantity(int currentStock, int threshold)
    {
        int needed = _maxLevel - currentStock;
        return needed > 0 ? needed : 0;
    }
}
