namespace InventoryManagementSystem.Entities;

public enum MovementType
{
    Addition,
    Removal,
    TransferOut,
    TransferIn
}

public class StockMovement
{
    public string ProductSku { get; }
    public int Quantity { get; }
    public string WarehouseId { get; }
    public string? CounterpartWarehouseId { get; }
    public MovementType Type { get; }
    public DateTime Timestamp { get; }

    public StockMovement(string productSku, int quantity, string warehouseId, string? counterpartWarehouseId, MovementType type)
    {
        ProductSku = productSku;
        Quantity = quantity;
        WarehouseId = warehouseId;
        CounterpartWarehouseId = counterpartWarehouseId;
        Type = type;
        Timestamp = DateTime.UtcNow;
    }

    public override string ToString()
    {
        return $"[{Timestamp:O}] {Type} | SKU: {ProductSku} | Qty: {Quantity} | Warehouse: {WarehouseId} | Counterpart: {CounterpartWarehouseId ?? "N/A"}";
    }
}
