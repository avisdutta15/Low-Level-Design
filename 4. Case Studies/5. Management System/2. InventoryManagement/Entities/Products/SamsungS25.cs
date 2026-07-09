using InventoryManagementSystem.Enums;

namespace InventoryManagementSystem.Entities.Products;

public class SamsungS25 : Product
{
    public SamsungS25(string sku, string name, double price , ProductCategory category)
        : base(sku, name, price, category)
    {
    }
}
