namespace InventoryManagementSystem.Observers;
public interface IAlertListener
{
    void OnLowStock(string warehouseId, string productSku, int currentQuantity, int threshold);
}
