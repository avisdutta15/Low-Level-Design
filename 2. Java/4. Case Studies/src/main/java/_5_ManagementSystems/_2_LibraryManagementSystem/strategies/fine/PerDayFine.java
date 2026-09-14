package _5_ManagementSystems._2_LibraryManagementSystem.strategies.fine;

import _5_ManagementSystems._2_LibraryManagementSystem.entities.BorrowSlip;

import java.time.Duration;

public class PerDayFine implements FineStrategy{
    private final double perDayFine;

    public PerDayFine(){
        this.perDayFine = 10.00;
    }

    @Override
    public double calculateFine(BorrowSlip borrowSlip) {
        Duration overdueDuration = Duration.between(borrowSlip.getDueDate(), borrowSlip.getReturnDate());
        long daysLate = overdueDuration.toDays();
        return daysLate > 0 ? daysLate * perDayFine : 0.00;
    }
}
