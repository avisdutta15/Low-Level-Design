package _5_ManagementSystems._2_LibraryManagementSystem.entities;

import java.util.*;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.atomic.AtomicInteger;

public class Member {
    private final String name;
    private final String id;
    private final String contactInfo;
    private final AtomicInteger currentBorrowCount;
    private final List<BorrowSlip> borrowHistory;

    public Member(String name, String id, String contactInfo) {
        this.name = name;
        this.id = id;
        this.contactInfo = contactInfo;
        this.currentBorrowCount = new AtomicInteger(0);
        this.borrowHistory = new CopyOnWriteArrayList<>();
    }

    public boolean canBorrow(int maxBorrowCount){
        return currentBorrowCount.get() < maxBorrowCount;
    }

    public boolean incrementBorrowCount(int maxBorrowCount){
        // CAS Loop!
        while(true){
            int current = currentBorrowCount.get();
            if(current >= maxBorrowCount) return false;
            if(currentBorrowCount.compareAndSet(current, current + 1)) return true;
        }
    }

    public void decrementBorrowCount(){
        currentBorrowCount.decrementAndGet();
    }

    public void addToborrowHistory(BorrowSlip borrowSlip){
        borrowHistory.add(borrowSlip);
    }

    // getters and setters
    public String getName() { return name; }
    public String getId() { return id; }
    public String getContactInfo() { return contactInfo; }
    public int getCurrentBorrowCount() { return currentBorrowCount.get(); }
    public List<BorrowSlip> getBorrowHistory() { return borrowHistory; }
}
