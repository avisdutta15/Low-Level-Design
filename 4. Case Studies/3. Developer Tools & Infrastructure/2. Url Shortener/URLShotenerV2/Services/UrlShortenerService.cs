using URLShotenerV2.Entities;
using URLShotenerV2.Exceptions;
using URLShotenerV2.Observers;
using URLShotenerV2.Repository;
using URLShotenerV2.Strategies;

namespace URLShotenerV2.Services;

public class UrlShortenerService : IUrlSubject
{
    private readonly IUrlRepository _repository;
    private readonly IUrlGeneratorStrategy _generatorStrategy;
    private readonly List<IUrlObserver> _observers = new();
    private readonly object _lock = new();


    public UrlShortenerService(IUrlRepository repository, IUrlGeneratorStrategy generator)
    {
        _repository = repository;
        _generatorStrategy = generator;
    }

    public async Task<string> ShortenUrlAsync(string longUrl, string? customAlias = null, DateTimeOffset? expirationTime = null)
    {
        // 1. Check if any customAlias has been provided.
        //  -> If Yes then check if it's available.
        //  ->                   If not available return custom error
        //  ->                   If available then use this as short url

        string aliasToUse;

        if (!string.IsNullOrWhiteSpace(customAlias))
        {
            if (await _repository.AliasExistsAsync(customAlias))
                throw new AliasAlreadyTakenException(customAlias);

            aliasToUse = customAlias;
        }
        else
        {   // 2. Generate a unique short code.
            //    Note we do this in a loop
            //    because there is a small chance that the generated
            //    url might not be unique.
            int maxRetries = 5;
            do
            {
                aliasToUse = _generatorStrategy.Generate(longUrl);
                maxRetries--;
                if (maxRetries == 0) throw new Exception("Failed to generate a unique alias.");
            } while (await _repository.AliasExistsAsync(aliasToUse));
        }

        // 3. Create the entity and store in repository
        var urlEntity = new UrlEntity(originalUrl: longUrl, shortUrl: aliasToUse, expirationTime: expirationTime);
        await _repository.AddUrlEntityAsync(urlEntity);

        // CLASSIC OBSERVER: Notify all attached observers
        NotifyObservers(UrlEventType.UrlCreated, urlEntity);

        return aliasToUse;
    }

    public async Task<string> ResolveUrlAsync(string alias)
    {
        var entity = await _repository.GetEntityByAliasAsync(alias);

        if (entity == null) throw new UrlNotFoundException(alias);
        if (entity.IsExpired()) throw new UrlExpiredException(alias);

        entity.RecordVisit();
        await _repository.UpdateEntityAsync(entity);

        // CLASSIC OBSERVER: Notify all attached observers
        NotifyObservers(UrlEventType.UrlVisited, entity);

        return entity.OriginalUrl;
    }

    public void Attach(IUrlObserver observer)
    {
        lock (_observers)
        {
            if (!_observers.Contains(observer))
                _observers.Add(observer);
        }
    }

    public void Detach(IUrlObserver observer)
    {
        lock (_observers)
        {
            _observers.Remove(observer);
        }
    }


    //  Notify using snapshot of current observers to avoid concurrency issues.
    //  If one of the observers takes 2 seconds to process the event, your entire
    //  UrlShortenerService freezes for 2 seconds. No other thread can Attach,
    //  Detach, or Notify during that time.

    //  Notify using snapshot of current observers to avoid concurrency issues.
    //  Background threads are used to prevent the UrlShortenerService from freezing.
    public void NotifyObservers(UrlEventType urlEventType, UrlEntity entity)
    {
        // 1. Snapshot to avoid holding lock during callbacks
        List<IUrlObserver> snapshot;
        lock (_lock)
        {
            snapshot = new List<IUrlObserver>(_observers);
        }

        // 2. Iterate over the snapshot and dispatch each notification to a ThreadPool thread
        foreach (var observer in snapshot)
        {
            // Fire and forget! The main thread moves on instantly.
            Task.Run(() =>
            {
                try
                {
                    observer.OnUrlEvent(urlEventType, entity);
                }
                catch (Exception ex)
                {
                    // CRITICAL: Always catch exceptions in fire-and-forget tasks!
                    // Otherwise, a failing observer goes unnoticed, but wrapping it
                    // ensures it doesn't crash the application or affect other observers.
                    Console.WriteLine($"[Warning] Observer {observer.GetType().Name} failed: {ex.Message}");
                }
            });
        }
    }
}
