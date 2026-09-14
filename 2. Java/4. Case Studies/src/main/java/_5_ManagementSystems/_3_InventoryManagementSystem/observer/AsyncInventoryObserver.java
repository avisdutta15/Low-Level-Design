package _5_ManagementSystems._3_InventoryManagementSystem.observer;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class AsyncInventoryObserver implements InventoryObserver {
    private final InventoryObserver delegate;
    private final ExecutorService executor;

    public AsyncInventoryObserver(InventoryObserver delegate) {
        this.delegate = delegate;
        this.executor = Executors.newSingleThreadExecutor();
    }

    @Override
    public void onEvent(InventoryEventData data) {
        executor.submit(() -> delegate.onEvent(data));
    }

    public void shutdown() { executor.shutdown(); }
}
