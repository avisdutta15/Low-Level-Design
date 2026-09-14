package _5_ManagementSystems._2_LibraryManagementSystem;

import _5_ManagementSystems._2_LibraryManagementSystem.entities.*;
import _5_ManagementSystems._2_LibraryManagementSystem.entities.payment.CreditCardPayment;
import _5_ManagementSystems._2_LibraryManagementSystem.entities.search.SearchCriteria;
import _5_ManagementSystems._2_LibraryManagementSystem.observer.*;
import _5_ManagementSystems._2_LibraryManagementSystem.services.BookSearchService;
import _5_ManagementSystems._2_LibraryManagementSystem.strategies.fine.PerDayFine;
import _5_ManagementSystems._2_LibraryManagementSystem.strategies.payment.PaymentModeStrategy;
import _5_ManagementSystems._2_LibraryManagementSystem.strategies.payment.PaymentProcessor;

import java.util.List;
import java.util.concurrent.*;

public class Main {
    public static void main(String[] args) throws InterruptedException {
        Library library = Library.getInstance(
                new PaymentProcessor(), new PerDayFine(), new BookSearchService(), 3, 14);

        // Subscribe sync observers
        NotificationService emailService = new NotificationService("email-service");
        AvailabilityBoard board = new AvailabilityBoard("main-entrance");
        library.subscribe(emailService);
        library.subscribe(board);

        testBasicBorrowReturn(library);
        testBorrowLimit(library);
        testNoCopiesAvailable(library);
        testSearch(library);
        testObserverUnsubscribe(library, emailService);
        testAsyncObserver(library);
        testConcurrentBorrows(library);
        testConcurrentReturnRace(library);
    }

    private static void testBasicBorrowReturn(Library library) throws InterruptedException {
        System.out.println("\n--- Basic Borrow/Return ---");
        library.addBook(new Book("978-0-13-468599-1", "Clean Code", "Robert C. Martin", 2008, 3));
        library.addBook(new Book("978-0-201-63361-0", "Design Patterns", "GoF", 1994, 2));
        library.registerMember(new Member("Alice", "M001", "alice@email.com"));

        BorrowSlip slip = library.borrow("M001", "978-0-13-468599-1");
        System.out.println("Borrowed, slip: " + slip.getId());

        Thread.sleep(500);

        PaymentModeStrategy card = new CreditCardPayment("4111111111111111", "12/26", "123");
        library.returnBook(slip, card);
        System.out.println("Returned successfully");
    }

    private static void testBorrowLimit(Library library) {
        System.out.println("\n--- Borrow Limit Test (max 3) ---");
        library.registerMember(new Member("Bob", "M002", "bob@email.com"));

        library.borrow("M002", "978-0-13-468599-1");
        library.borrow("M002", "978-0-201-63361-0");
        library.addBook(new Book("978-0-13-235088-4", "Effective Java", "Joshua Bloch", 2018, 1));
        library.borrow("M002", "978-0-13-235088-4");

        try {
            library.borrow("M002", "978-0-13-468599-1"); // 4th — should fail
        } catch (RuntimeException e) {
            System.out.println("Expected: " + e.getMessage());
        }
    }

    private static void testNoCopiesAvailable(Library library) {
        System.out.println("\n--- No Copies Available Test ---");
        library.addBook(new Book("978-0-00-000001-0", "Single Copy Book", "Author X", 2020, 1));
        library.registerMember(new Member("Charlie", "M003", "charlie@email.com"));
        library.registerMember(new Member("Diana", "M004", "diana@email.com"));

        library.borrow("M003", "978-0-00-000001-0");
        try {
            library.borrow("M004", "978-0-00-000001-0"); // no copies left
        } catch (RuntimeException e) {
            System.out.println("Expected: " + e.getMessage());
        }
    }

    private static void testSearch(Library library) {
        System.out.println("\n--- Search Test ---");
        List<Book> byAuthor = library.search(new SearchCriteria().setAuthor("martin"));
        System.out.println("Search 'author=martin': " + byAuthor.size() + " result(s)");
        byAuthor.forEach(b -> System.out.println("  → " + b.getTitle()));

        List<Book> available = library.search(new SearchCriteria().setAvailableOnly(true));
        System.out.println("Available books: " + available.size());
    }

    private static void testObserverUnsubscribe(Library library, LibraryObserver emailService) {
        System.out.println("\n--- Observer Unsubscribe Test ---");
        library.unsubscribe(emailService);
        System.out.println("Unsubscribed email-service. Next events should only show on board:");
        library.addBook(new Book("978-0-00-000002-0", "Observer Test Book", "Author Y", 2021, 1));
    }

    private static void testAsyncObserver(Library library) throws InterruptedException {
        System.out.println("\n--- Async Observer Test ---");

        // Wrap a NotificationService in async
        NotificationService smsService = new NotificationService("sms-service");
        AsyncLibraryObserver asyncSms = new AsyncLibraryObserver(smsService);
        library.subscribe(asyncSms);

        library.addBook(new Book("978-0-00-000003-0", "Async Book", "Author Z", 2022, 2));
        library.registerMember(new Member("Eve", "M005", "eve@email.com"));
        BorrowSlip slip = library.borrow("M005", "978-0-00-000003-0");

        // Give async observer time to process
        Thread.sleep(500);

        PaymentModeStrategy card = new CreditCardPayment("4222222222222222", "01/27", "456");
        library.returnBook(slip, card);

        Thread.sleep(500);

        asyncSms.shutdown();
        library.unsubscribe(asyncSms);
        System.out.println("Async observer shutdown");
    }

    private static void testConcurrentBorrows(Library library) throws InterruptedException {
        System.out.println("\n--- Concurrent Borrow Test (5 threads, 1 copy) ---");
        library.addBook(new Book("978-0-00-000004-0", "Concurrency in Practice", "Brian Goetz", 2006, 1));

        String[] memberIds = {"M001", "M002", "M003", "M004", "M005"};
        // Free up members who may be at borrow limit — use fresh members
        for (int i = 6; i <= 10; i++) {
            library.registerMember(new Member("Racer-" + i, "R00" + i, "racer" + i + "@email.com"));
        }
        String[] racerIds = {"R006", "R007", "R008", "R009", "R0010"};

        ExecutorService executor = Executors.newFixedThreadPool(5);
        for (int i = 0; i < 5; i++) {
            int idx = i;
            executor.submit(() -> {
                try {
                    library.borrow(racerIds[idx], "978-0-00-000004-0");
                    System.out.println("[Thread-" + (idx + 1) + "] " + racerIds[idx] + " borrowed successfully");
                } catch (RuntimeException e) {
                    System.out.println("[Thread-" + (idx + 1) + "] " + racerIds[idx] + " failed: " + e.getMessage());
                }
            });
        }
        executor.shutdown();
        executor.awaitTermination(5, TimeUnit.SECONDS);
        System.out.println("Only 1 thread should have succeeded above");
    }

    private static void testConcurrentReturnRace(Library library) throws InterruptedException {
        System.out.println("\n--- Concurrent Return Race Test ---");
        library.addBook(new Book("978-0-00-000005-0", "Race Condition Book", "Author RC", 2023, 1));
        library.registerMember(new Member("Racer-A", "RA01", "ra@email.com"));

        BorrowSlip slip = library.borrow("RA01", "978-0-00-000005-0");
        System.out.println("Borrowed, slip: " + slip.getId());

        Thread.sleep(500);

        // Two threads race to return the same slip
        Thread t1 = new Thread(() -> {
            try {
                library.returnBook(slip, new CreditCardPayment("4111111111111111", "12/26", "123"));
                System.out.println("[Thread-1] Returned successfully");
            } catch (RuntimeException e) {
                System.out.println("[Thread-1] Failed: " + e.getMessage());
            }
        });

        Thread t2 = new Thread(() -> {
            try {
                library.returnBook(slip, new CreditCardPayment("4222222222222222", "01/27", "456"));
                System.out.println("[Thread-2] Returned successfully");
            } catch (RuntimeException e) {
                System.out.println("[Thread-2] Failed: " + e.getMessage());
            }
        });

        t1.start();
        t2.start();
        t1.join();
        t2.join();
        System.out.println("Only one thread should have succeeded above");
    }
}
