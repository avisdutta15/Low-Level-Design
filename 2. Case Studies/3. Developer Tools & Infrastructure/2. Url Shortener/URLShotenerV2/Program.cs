using URLShotenerV2.Observers;
using URLShotenerV2.Repository;
using URLShotenerV2.Services;
using URLShotenerV2.Strategies;

public class Program
{
    public static async Task Main()
    {
        // 1. Setup Dependencies
        IUrlRepository repository = new InMemoryUrlRepository();

        // Using the CounterBase62 strategy as requested
        IUrlGeneratorStrategy generator = new CounterBasedBase62UrlGeneratorStrategy();

        // 2. Setup the Subject (Service Orchestrator)
        UrlShortenerService service = new UrlShortenerService(repository, generator);

        // 3. Setup the Observer (Analytics Tracker)
        // It automatically attaches itself to the service's events.
        RealTimeAnalyticsTracker analytics = new RealTimeAnalyticsTracker();
        service.Attach(analytics);

        Console.WriteLine("--- Shortening URLs ---");

        // Auto-generated alias
        string alias1 = await service.ShortenUrlAsync("https://www.google.com");
        Console.WriteLine($"Shortened Google: {alias1}");

        // Custom alias
        string alias2 = await service.ShortenUrlAsync("https://www.microsoft.com", "msft");
        Console.WriteLine($"Shortened Microsoft (Custom): {alias2}");

        // URL with Expiration (Expires in 5 minutes)
        string alias3 = await service.ShortenUrlAsync("https://www.apple.com", expirationTime: DateTimeOffset.UtcNow.AddMinutes(5));
        Console.WriteLine($"Shortened Apple (With Expiration): {alias3}");

        Console.WriteLine("\n--- Resolving URLs ---");

        // Resolve alias1 twice to simulate multiple clicks
        string original1 = await service.ResolveUrlAsync(alias1);
        Console.WriteLine($"Resolved {alias1} to: {original1}");
        await service.ResolveUrlAsync(alias1);

        // Resolve alias2 once
        string originalMsft = await service.ResolveUrlAsync("msft");
        Console.WriteLine($"Resolved msft to: {originalMsft}");

        Console.WriteLine("\n--- System Analytics ---");

        // Fetch real-time analytics processed entirely by the Observer
        var stats = analytics.GetAnalytics();
        Console.WriteLine($"Total Links Created: {stats.TotalLinks}");
        Console.WriteLine($"Total Clicks Tracked: {stats.TotalClicks}");
        Console.WriteLine($"Active Links (Not Expired): {stats.ActiveLinks}");
    }
}