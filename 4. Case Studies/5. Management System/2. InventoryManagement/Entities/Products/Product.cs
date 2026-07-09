using InventoryManagementSystem.Enums;

namespace InventoryManagementSystem.Entities.Products;

public class Product
{
    public string Sku { get; }
    public string Name { get; }
    public double Price { get; }
    public ProductCategory Category { get; }

    public Product(string sku, string name, double price, ProductCategory category)
    {
        Sku = sku;
        Name = name;
        Price = price;
        Category = category;
    }
}
