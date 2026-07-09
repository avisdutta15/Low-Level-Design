using InventoryManagementSystem.Entities.Products;
using System.Collections.Concurrent;

namespace InventoryManagementSystem.Entities;

public class Warehouse
{
    public string Id { get; }
    public string Name { get; }
    public string Location { get; }

    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, Product> _catalog = new();
    private readonly ConcurrentDictionary<string, int> _stock = new();

    public Warehouse(string id, string name, string location)
    {
        Id = id;
        Name = name;
        Location = location;
    }

    public void AddStock(Product product, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        lock (_lock)
        {
            _catalog.TryAdd(product.Sku, product);
            _stock.AddOrUpdate(
                key:product.Sku, 
                addValue: quantity, 
                updateValueFactory:(_, existing) => existing + quantity
            );
        }
    }

    public bool RemoveStock(string sku, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        lock (_lock)
        {
            // If product does not exist return false
            if (!_stock.TryGetValue(sku, out int current))
                return false;

            // If the quantity requested is greater than available stock, return false
            if(current < quantity)
                return false;

            // Decrease the stock
            _stock[sku] = current - quantity;
            return true;
        }
    }

    public int GetStockLevel(string sku)
    {
        return _stock.TryGetValue(sku, out int qty) == true? qty : 0;
    }

    public Product? GetProduct(string sku)
    {
        return _catalog.TryGetValue(sku, out Product? product) == true ? product : null;
    }

    public bool HasProduct(string sku)
    {
        return _catalog.ContainsKey(sku);
    }
}
