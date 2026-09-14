package _5_ManagementSystems._2_LibraryManagementSystem.observer;

public class LibraryEventData {
    private final LibraryEvent event;
    private final String bookTitle;
    private final String isbn;
    private final String memberName;
    private final double fine;

    public LibraryEventData(LibraryEvent event, String bookTitle, String isbn, String memberName, double fine) {
        this.event = event;
        this.bookTitle = bookTitle;
        this.isbn = isbn;
        this.memberName = memberName;
        this.fine = fine;
    }

    public LibraryEvent getEvent() { return event; }
    public String getBookTitle() { return bookTitle; }
    public String getIsbn() { return isbn; }
    public String getMemberName() { return memberName; }
    public double getFine() { return fine; }
}
