using System.Collections.Immutable;
using URLShotenerV1.Entities;
using URLShotenerV1.Exceptions;
using URLShotenerV1.Observers;
using URLShotenerV1.Repository;
using URLShotenerV1.Strategies;

namespace URLShotenerV1.Services;

public class UrlShortenerService : ISubject
{
    private readonly IUrlRepository _repository;
    private readonly IUrlGeneratorStrategy _generatorStrategy;
    private ImmutableHashSet<IObserver> _observers = ImmutableHashSet<IObserver>.Empty;
    public UrlShortenerService(IUrlRepository repository, IUrlGeneratorStrategy generatorStrategy)
    {
        _repository = repository;
        _generatorStrategy = generatorStrategy;
    }

    // Core Methods

    public string ShortenUrl(string longUrl, string? customAlias, DateTimeOffset? expirationTime = null)
    {
        // 1. Check if any customAlias has been provided.
        //  -> If Yes then check if it's available.
        //  ->                   If not available return custom error
        //  ->                   If available then use this as short url
        string aliasToUse;
        if (!string.IsNullOrEmpty(customAlias))
        {
            if (_repository.ShortUrlExists(customAlias))
                throw new AliasAlreadyTakenException(customAlias);
            aliasToUse = customAlias;
        }
        else
        {
            // 2. Generate a unique short code.
            //    Note we do this in a loop
            //    because there is a small chance that the generated
            //    url might not be unique (example - if the strategy is hash based, we might have collision).
            int maxRetries = 5;
            do
            {
                aliasToUse = _generatorStrategy.Generate(longUrl);
                maxRetries--;
                if (maxRetries == 0) 
                    throw new Exception("Failed to generate a unique alias.");
            } while (_repository.ShortUrlExists(aliasToUse) == true);
        }

        // 3. Create the entity and store in repository
        var urlEntry = new UrlEntity(originalUrl: longUrl, shortUrl: aliasToUse, expirationTime: expirationTime);
        _repository.AddUrlEntity(urlEntry);
        Notify(UrlEventType.CREATED, urlEntry);

        // 4. Return the aliasToUse
        return aliasToUse;    
    }

    public string RedirectUrl(string shortUrl)
    {
        // 1. Validate the short Url
        if(string.IsNullOrEmpty(shortUrl))
            throw new InvalidShortUrlException(shortUrl);

        // 2. Check if the shortUrl exists or not
        // 2.1 Check if the shortUrl has expired or not
        var entity = _repository.GetEntityByAlias(shortUrl);
        if(entity == null)
            throw new UrlNotFoundException(shortUrl);
        if (entity.IsExpired())
        {
            _repository.DeleteEntity(entity);
            throw new UrlExpiredException(shortUrl);
        }

        // 3. Record the visit
        entity.RecordVisit();
        _repository.UpdateEntity(entity);
        Notify(UrlEventType.VISITED, entity);

        // 4. Return the long url
        return entity.OriginalUrl;
    }

    // Subscription Methods

    public void Subscribe(IObserver observer)
    {
        ImmutableInterlocked.Update(ref _observers, list=>list.Add(observer));
    }

    public void Unsubscribe(IObserver observer)
    {
        ImmutableInterlocked.Update(ref _observers, list => list.Remove(observer));
    }

    public void Notify(UrlEventType eventType, UrlEntity entity)
    {
        foreach(var observer in _observers)
        {
            observer.Update(eventType, entity);
        }
    }
}
