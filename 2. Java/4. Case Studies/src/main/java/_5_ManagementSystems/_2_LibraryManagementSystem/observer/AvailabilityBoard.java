package _5_ManagementSystems._2_LibraryManagementSystem.observer;

public class AvailabilityBoard implements LibraryObserver {
    private final String boardId;

    public AvailabilityBoard(String boardId) { this.boardId = boardId; }

    @Override
    public void onEvent(LibraryEventData data) {
        if (data.getEvent() == LibraryEvent.BOOK_BORROWED || data.getEvent() == LibraryEvent.BOOK_RETURNED) {
            System.out.println("[Board:" + boardId + "] '" + data.getBookTitle() + "' (ISBN: " + data.getIsbn() + ") status updated");
        }
    }
}
