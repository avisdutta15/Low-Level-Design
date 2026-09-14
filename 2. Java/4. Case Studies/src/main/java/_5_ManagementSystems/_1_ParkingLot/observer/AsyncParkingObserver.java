package _5_ManagementSystems._1_ParkingLot.observer;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Wraps any ParkingObserver to make it async with ordering guarantee.
 * Uses a single-thread executor so events are processed in FIFO order
 * without blocking the caller (park/unpark thread).
 *
 * Executors.newSingleThreadExecutor() creates a pool with exactly 1 thread and
 * an unbounded queue (LinkedBlockingQueue). Tasks are processed one at a time, in submission order (FIFO).
 *  - notifyObservers(CAR PARKED)    → submits to observer's queue → [CAR PARKED]
 *  - notifyObservers(BIKE PARKED)   → submits to observer's queue → [CAR PARKED, BIKE PARKED]
 *  - notifyObservers(CAR UNPARKED)  → submits to observer's queue → [CAR PARKED, BIKE PARKED, CAR UNPARKED]
 *  Since each observer has one thread, and each thread has their own BlockingQueue,
 *  the events for each observer is ordered.
 *
 *  It's the same concept as a BlockingQueue with one consumer thread — just wrapped by the JDK's ExecutorService.
 */
public class AsyncParkingObserver implements ParkingObserver {
    private final ParkingObserver delegate;
    private final ExecutorService executor;

    public AsyncParkingObserver(ParkingObserver delegate) {
        this.delegate = delegate;
        this.executor = Executors.newSingleThreadExecutor();
    }

    @Override
    public void onEvent(ParkingEvent event) {
        executor.submit(() -> delegate.onEvent(event));
    }

    public void shutdown() {
        executor.shutdown();
    }
}
