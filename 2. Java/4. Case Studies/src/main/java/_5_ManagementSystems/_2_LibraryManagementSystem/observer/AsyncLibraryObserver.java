package _5_ManagementSystems._2_LibraryManagementSystem.observer;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Wraps any LibraryObserver to make it async with ordering guarantee.
 * Uses a single-thread executor so events are processed in FIFO order
 * without blocking the caller (borrow/return thread).
 *
 * Each observer gets its own single-thread executor + unbounded queue.
 * Events are submitted to the queue and processed one at a time, in order.
 */
public class AsyncLibraryObserver implements LibraryObserver {
    private final LibraryObserver delegate;
    private final ExecutorService executor;

    public AsyncLibraryObserver(LibraryObserver delegate) {
        this.delegate = delegate;
        this.executor = Executors.newSingleThreadExecutor();
    }

    @Override
    public void onEvent(LibraryEventData eventData) {
        executor.submit(() -> delegate.onEvent(eventData));
    }

    public void shutdown() { executor.shutdown(); }
}
