package _5_ManagementSystems._3_InventoryManagementSystem.strategies;

import _5_ManagementSystems._3_InventoryManagementSystem.entities.Product;

public interface ReplenishmentStrategy {
    int calculateReorderQuantity(Product product);
}
