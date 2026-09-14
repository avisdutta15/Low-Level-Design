package _5_ManagementSystems._1_ParkingLot.observer;

import _5_ManagementSystems._1_ParkingLot.enums.VehicleType;

import java.util.HashMap;
import java.util.Map;

public class DashboardDisplay implements ParkingObserver {
    private final String name;
    private final Map<VehicleType, Integer> available = new HashMap<>();
    private final Map<VehicleType, Integer> occupied = new HashMap<>();

    public DashboardDisplay(String name) {
        this.name = name;
        for (VehicleType type : VehicleType.values()) {
            available.put(type, 0);
            occupied.put(type, 0);
        }
    }

    @Override
    public void onEvent(ParkingEvent event) {
        VehicleType type = event.vehicleType();

        if (event.eventType() == EventType.SPOT_ADDED) {
            available.put(type, available.get(type) + 1);
        } else if (event.eventType() == EventType.PARKED) {
            available.put(type, available.get(type) - 1);
            occupied.put(type, occupied.get(type) + 1);
        } else {
            available.put(type, available.get(type) + 1);
            occupied.put(type, occupied.get(type) - 1);
        }

        display();
    }

    private void display() {
        System.out.println("\n===== [" + name + "] Dashboard =====");
        for (VehicleType type : VehicleType.values()) {
            System.out.printf("  %s → Available: %d | Booked: %d%n",
                    type, available.get(type), occupied.get(type));
        }
        System.out.println("====================================\n");
    }
}
