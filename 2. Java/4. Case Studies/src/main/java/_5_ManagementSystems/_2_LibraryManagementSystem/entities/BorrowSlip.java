package _5_ManagementSystems._2_LibraryManagementSystem.entities;

import java.time.LocalDateTime;
import java.util.UUID;

public class BorrowSlip {
    private final String id;
    private final String memberId;
    private final String isbn;
    private final int loanDurationDays;
    private final LocalDateTime borrowDate;
    private final LocalDateTime dueDate;
    private volatile LocalDateTime returnDate = null;

    public BorrowSlip(String memberId, String isbn, int loanDurationDays) {
        this.id = UUID.randomUUID().toString();
        this.memberId = memberId;
        this.isbn = isbn;
        this.loanDurationDays = loanDurationDays;
        this.borrowDate = LocalDateTime.now();
        this.dueDate = borrowDate.plusDays(loanDurationDays);
    }

    public String getId() { return id; }
    public String getMemberId() { return memberId; }
    public String getIsbn() { return isbn; }
    public int getLoanDurationDays() { return loanDurationDays; }
    public LocalDateTime getBorrowDate() { return borrowDate; }
    public LocalDateTime getReturnDate() { return returnDate; }
    public LocalDateTime getDueDate() { return dueDate; }
    public void setReturnDate(LocalDateTime returnDate) { this.returnDate = returnDate; }
}
