package _5_ManagementSystems._3_InventoryManagementSystem.entities;

public class Product {
    private final String sku;
    private final String name;
    private final String category;
    private final int lowStockThreshold;

    public Product(String sku, String name, String category, int lowStockThreshold) {
        this.sku = sku;
        this.name = name;
        this.category = category;
        this.lowStockThreshold = lowStockThreshold;
    }

    public String getSku() { return sku;}
    public String getName() { return name;}
    public String getCategory() { return category;}
    public int getLowStockThreshold() {return lowStockThreshold;}
}
