package _5_ManagementSystems._3_InventoryManagementSystem.strategies;

import _5_ManagementSystems._3_InventoryManagementSystem.entities.Product;

public class FixedAmountReplenishment implements ReplenishmentStrategy{
    private final int fixedQuantity;

    public FixedAmountReplenishment(int fixedQuantity){
        this.fixedQuantity = fixedQuantity;
    }

    @Override
    public int calculateReorderQuantity(Product product) {
        return fixedQuantity;
    }
}
