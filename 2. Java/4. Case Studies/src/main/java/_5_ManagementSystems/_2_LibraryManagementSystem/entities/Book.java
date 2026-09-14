package _5_ManagementSystems._2_LibraryManagementSystem.entities;

import java.util.concurrent.atomic.AtomicInteger;

public class Book {
    private final String isbn;
    private final String title;
    private final String author;
    private final int publicationYear;
    private final AtomicInteger availableCopies;

    public Book(String isbn, String title, String author, int publicationYear, int totalCopies) {
        this.isbn = isbn;
        this.title = title;
        this.author = author;
        this.publicationYear = publicationYear;
        this.availableCopies = new AtomicInteger(totalCopies);
    }

    public boolean tryBorrow() {
        // CAS Loop!
        while (true) {
            int current = availableCopies.get();
            if (current <= 0) return false;
            if (availableCopies.compareAndSet(current, current - 1)) return true;
        }
    }

    public void returnBook() { availableCopies.incrementAndGet(); }

    public String getIsbn() { return isbn; }
    public String getTitle() { return title; }
    public String getAuthor() { return author; }
    public int getPublicationYear() { return publicationYear; }
    public int getAvailableCopies() { return availableCopies.get(); }
}
