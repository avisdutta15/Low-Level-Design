package _5_ManagementSystems._2_LibraryManagementSystem.strategies.fine;

import _5_ManagementSystems._2_LibraryManagementSystem.entities.BorrowSlip;

public interface FineStrategy {
    public double calculateFine(BorrowSlip borrowSlip);
}
