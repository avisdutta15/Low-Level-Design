package _5_ManagementSystems._2_LibraryManagementSystem.entities;

import _5_ManagementSystems._2_LibraryManagementSystem.entities.payment.PaymentResult;
import _5_ManagementSystems._2_LibraryManagementSystem.entities.search.SearchCriteria;
import _5_ManagementSystems._2_LibraryManagementSystem.enums.PaymentStatus;
import _5_ManagementSystems._2_LibraryManagementSystem.observer.*;
import _5_ManagementSystems._2_LibraryManagementSystem.services.BookSearchService;
import _5_ManagementSystems._2_LibraryManagementSystem.strategies.fine.FineStrategy;
import _5_ManagementSystems._2_LibraryManagementSystem.strategies.payment.PaymentModeStrategy;
import _5_ManagementSystems._2_LibraryManagementSystem.strategies.payment.PaymentProcessor;

import java.time.LocalDateTime;
import java.util.*;
import java.util.concurrent.*;

public class Library {
    private static volatile Library instance;
    private static final Object lock = new Object();

    private final Map<String, Book> catalog;
    private final Map<String, Member> members;
    private final Map<String, BorrowSlip> activeLoans;
    private final List<LibraryObserver> observers;

    private final PaymentProcessor paymentProcessor;
    private final FineStrategy fineStrategy;
    private final BookSearchService bookSearchService;

    private final int maxBorrowCount;
    private final int loanDurationDays;

    private Library(PaymentProcessor paymentProcessor, FineStrategy fineStrategy,
                    BookSearchService bookSearchService, int maxBorrowCount, int loanDurationDays) {
        this.catalog = new ConcurrentHashMap<>();
        this.members = new ConcurrentHashMap<>();
        this.activeLoans = new ConcurrentHashMap<>();
        this.observers = new CopyOnWriteArrayList<>();
        this.paymentProcessor = paymentProcessor;
        this.fineStrategy = fineStrategy;
        this.bookSearchService = bookSearchService;
        this.maxBorrowCount = maxBorrowCount;
        this.loanDurationDays = loanDurationDays;
    }

    public static Library getInstance(PaymentProcessor paymentProcessor, FineStrategy fineStrategy,
                                      BookSearchService bookSearchService, int maxBorrowCount, int loanDurationDays) {
        if (instance == null) {
            synchronized (lock) {
                if (instance == null) {
                    instance = new Library(paymentProcessor, fineStrategy, bookSearchService, maxBorrowCount, loanDurationDays);
                }
            }
        }
        return instance;
    }

    // ---- Observer --------
    public void subscribe(LibraryObserver observer) { observers.add(observer); }
    public void unsubscribe(LibraryObserver observer) { observers.remove(observer); }
    private void notifyObservers(LibraryEventData eventData) {
        for (LibraryObserver observer : observers) {
            observer.onEvent(eventData);
        }
    }

    // ---- Catalog management --------
    public void addBook(Book book) {
        catalog.put(book.getIsbn(), book);
        notifyObservers(new LibraryEventData(LibraryEvent.NEW_BOOK_ADDED, book.getTitle(), book.getIsbn(), null, 0));
    }

    // ---- Member management --------
    public void registerMember(Member member) { members.putIfAbsent(member.getId(), member); }

    // ---- Borrow --------
    public BorrowSlip borrow(String memberId, String isbn) {
        // validate inputs
        Book book = catalog.get(isbn);
        Member member = members.get(memberId);
        if (book == null) throw new RuntimeException("Book not found: " + isbn);
        if (member == null) throw new RuntimeException("Member not found: " + memberId);

        // CAS: check if member can borrow
        if (!member.incrementBorrowCount(maxBorrowCount)) {
            throw new RuntimeException("Borrow Limit Reached for Member: " + memberId);
        }

        // CAS: check book availability
        if (!book.tryBorrow()) {
            member.decrementBorrowCount(); // rollback
            throw new RuntimeException("No more copies left of book: " + isbn);
        }

        // generate borrow slip
        BorrowSlip borrowSlip = new BorrowSlip(memberId, isbn, loanDurationDays);
        activeLoans.put(borrowSlip.getId(), borrowSlip);
        member.addToborrowHistory(borrowSlip);

        notifyObservers(new LibraryEventData(LibraryEvent.BOOK_BORROWED, book.getTitle(), isbn, member.getName(), 0));
        return borrowSlip;
    }

    // ---- Return --------
    public void returnBook(BorrowSlip slip, PaymentModeStrategy paymentMode) {
        // basic validation
        BorrowSlip borrowSlip = activeLoans.remove(slip.getId());
        if (borrowSlip == null) throw new RuntimeException("Invalid borrow slip: " + slip.getId());
        Book book = catalog.get(borrowSlip.getIsbn());
        if (book == null) throw new RuntimeException("Invalid Book: " + borrowSlip.getIsbn());
        Member member = members.get(borrowSlip.getMemberId());

        // set the return date and calculate fine
        borrowSlip.setReturnDate(LocalDateTime.now());
        double fine = fineStrategy.calculateFine(borrowSlip);

        // payment and book return flow
        if (fine > 0.00) {
            // if no member needs to pay fine but no payment mode provided, throw exception
            if (paymentMode == null) {
                borrowSlip.setReturnDate(null);
                activeLoans.put(borrowSlip.getId(), borrowSlip);
                throw new RuntimeException("Fine of Rs." + fine + " due, payment required");
            }

            // make the payment
            PaymentResult result = paymentProcessor.processPayment(paymentMode, borrowSlip.getId(), fine);

            // if payment failed, make return date = null in slip and put back the slip into active loans
            if (result.getStatus() == PaymentStatus.FAILED) {
                borrowSlip.setReturnDate(null);
                activeLoans.put(borrowSlip.getId(), borrowSlip);
                return;
            }
        }

        // No fine OR payment succeeded — release resources
        boolean wasUnavailable = book.getAvailableCopies() == 0;
        book.returnBook();
        member.decrementBorrowCount();

        notifyObservers(new LibraryEventData(LibraryEvent.BOOK_RETURNED, book.getTitle(), book.getIsbn(), member.getName(), fine));

        if (wasUnavailable) {
            notifyObservers(new LibraryEventData(LibraryEvent.BOOK_AVAILABLE, book.getTitle(), book.getIsbn(), null, 0));
        }
    }

    // ---- Search --------
    public List<Book> search(SearchCriteria criteria) {
        return bookSearchService.search(catalog.values(), criteria);
    }
}
