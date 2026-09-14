package _5_ManagementSystems._2_LibraryManagementSystem.observer;

public class NotificationService implements LibraryObserver {
    private final String serviceId;

    public NotificationService(String serviceId) { this.serviceId = serviceId; }

    @Override
    public void onEvent(LibraryEventData data) {
        switch (data.getEvent()) {
            case BOOK_BORROWED -> System.out.println("[" + serviceId + "] " + data.getMemberName() + " borrowed '" + data.getBookTitle() + "'");
            case BOOK_RETURNED -> {
                String msg = "[" + serviceId + "] " + data.getMemberName() + " returned '" + data.getBookTitle() + "'";
                if (data.getFine() > 0) msg += " | Fine: ₹" + data.getFine();
                System.out.println(msg);
            }
            case BOOK_AVAILABLE -> System.out.println("[" + serviceId + "] '" + data.getBookTitle() + "' is now available!");
            case NEW_BOOK_ADDED -> System.out.println("[" + serviceId + "] New book added: '" + data.getBookTitle() + "'");
        }
    }
}
