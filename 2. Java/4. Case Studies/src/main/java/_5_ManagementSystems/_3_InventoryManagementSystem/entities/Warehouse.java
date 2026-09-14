package _5_ManagementSystems._3_InventoryManagementSystem.entities;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

public class Warehouse {
    private final String id;
    private final String name;
    private final String address;
    private final ConcurrentHashMap<String, StockEntry> productStockEntry;

    public Warehouse(String id, String name, String address) {
        this.id = id;
        this.name = name;
        this.address = address;
        this.productStockEntry = new ConcurrentHashMap<>();
    }

    public void addStock(String productId, int quantity){
        productStockEntry.computeIfAbsent(productId, k->new StockEntry(k)).addStock(quantity);
    }

    public boolean removeStock(String productId, int quantity){
        StockEntry stockEntry = productStockEntry.get(productId);
        if(stockEntry == null)  return false;   // if product does not exist then no entry. return null
        return stockEntry.removeQuantity(quantity);
    }

    public int getCurrentStock(String productId){
        StockEntry entry = productStockEntry.get(productId);
        if(entry == null)
            return 0;
        return entry.getCurrentStock();
    }

    // getters and setters
    public String getId() { return id; }
    public String getName() { return name; }
    public String getAddress() { return address; }
}
