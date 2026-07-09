namespace InventoryManagementSystem.Observers;

public class ConsoleAlertListener : IAlertListener
{
    public void OnLowStock(string warehouseId, string productSku, int currentQuantity, int threshold)
    {
        Console.WriteLine($"[LOW STOCK ALERT] Warehouse: {warehouseId}, Product: {productSku}, Quantity: {currentQuantity}, Threshold: {threshold}");
    }
}
