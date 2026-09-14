package _5_ManagementSystems._1_ParkingLot.strategies.pricing;

import _5_ManagementSystems._1_ParkingLot.entities.Ticket;
import _5_ManagementSystems._1_ParkingLot.enums.VehicleType;

import java.time.Duration;
import java.util.Map;
import java.util.concurrent.*;

public class EventBasedPricing implements ParkingFeeStrategy {
    private final Map<VehicleType, Double> rates = new ConcurrentHashMap<>();

    public EventBasedPricing() {
        rates.put(VehicleType.TRUCK, 200.00);
        rates.put(VehicleType.CAR, 100.00);
        rates.put(VehicleType.BIKE, 50.00);
    }

    @Override
    public double calculateFee(Ticket ticket) {
        Duration duration = Duration.between(ticket.getEntryTime(), ticket.getExitTime());

        // We should not do hours = Duration.toHours()
        // Duration.toHours() truncates — 45 minutes = 0 hours = ₹0 fee.
        // Use ceiling:
        //  totalMinutes / 60.00 = 0.12  (if hours < 1), round it up
        //  ceil(0.12) = 1
        // if the total time is 1hr 45m  then ceil (totalminutes) = 2
        long hours = (long)Math.ceil(duration.toMinutes() / 60.00);
        return hours * rates.get(ticket.getSpot().getVehicleType());
    }
}
