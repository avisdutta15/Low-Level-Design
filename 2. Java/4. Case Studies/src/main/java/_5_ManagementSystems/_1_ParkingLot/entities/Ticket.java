package _5_ManagementSystems._1_ParkingLot.entities;

import java.time.LocalDateTime;
import java.util.UUID;

public class Ticket {
    private final String id;
    private final ParkingFloor floor;
    private final ParkingSpot spot;
    private final LocalDateTime entryTime;
    private LocalDateTime exitTime;

    public Ticket(ParkingFloor floor, ParkingSpot spot) {
        this.id = UUID.randomUUID().toString();
        this.floor = floor;
        this.spot = spot;
        this.entryTime = LocalDateTime.now();
    }

    public String getId() {
        return id;
    }

    public ParkingFloor getFloor() {
        return floor;
    }

    public ParkingSpot getSpot() {
        return spot;
    }

    public LocalDateTime getEntryTime() {
        return entryTime;
    }

    public LocalDateTime getExitTime() {
        return exitTime;
    }

    public void setExitTime(LocalDateTime exitTime) {
        this.exitTime = exitTime;
    }
}
