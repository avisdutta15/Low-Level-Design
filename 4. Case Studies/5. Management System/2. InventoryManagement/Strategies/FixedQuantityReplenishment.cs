namespace InventoryManagementSystem.Strategies;

/// <summary>
/// Always reorders a fixed quantity regardless of current level.
/// </summary>
public class FixedQuantityReplenishment : IReplenishmentStrategy
{
    private readonly int _orderQuantity;

    public FixedQuantityReplenishment(int orderQuantity)
    {
        if (orderQuantity <= 0)
            throw new ArgumentException("Order quantity must be positive.", nameof(orderQuantity));
        _orderQuantity = orderQuantity;
    }

    public int CalculateReplenishmentQuantity(int currentStock, int threshold)
    {
        return _orderQuantity;
    }
}
