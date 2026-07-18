# URL Shortener - Low Level Design

## Table of Contents
- [1. Problem Statement](#1-problem-statement)
- [2. Functional Requirements](#2-functional-requirements)
- [3. Non-Functional Requirements](#3-non-functional-requirements)
- [4. Core Entities](#4-core-entities)
- [5. Relationships Between Entities](#5-relationships-between-entities)
- [6. Design Patterns Used](#6-design-patterns-used)
- [7. Class Diagram](#7-class-diagram)
- [8. Code for Each Class and Interface](#8-code-for-each-class-and-interface)
- [9. Evolution to V2](#9-evolution-to-v2)

---

## 1. Problem Statement

Design a URL Shortener service that converts long URLs into short, unique aliases. When a user accesses the short URL, the system redirects them to the original long URL. The system should support custom aliases, link expiration, click tracking, and real-time analytics.

---

## 2. Functional Requirements

1. **Shorten URL** — Given a long URL, generate a unique short alias.
2. **Custom Alias** — Allow users to provide a custom alias instead of auto-generating one.
3. **Redirect** — Given a short URL, resolve and return the original long URL.
4. **Link Expiration** — Support optional TTL (time-to-live) for short URLs.
5. **Click Tracking** — Track total clicks and last accessed time for each short URL.
6. **Analytics** — Provide real-time system-wide analytics (total links, total clicks, active links).
7. **Duplicate Custom Alias Detection** — Reject custom aliases that are already taken.

---

## 3. Non-Functional Requirements

1. **Thread Safety** — The system must handle concurrent reads/writes safely.
2. **Uniqueness** — Generated short URLs must be unique (retry on collision).
3. **Extensibility** — The URL generation strategy should be pluggable (Strategy pattern).
4. **Decoupled Analytics** — Analytics tracking should not be tightly coupled to core logic (Observer pattern).
5. **Performance** — Analytics counters should be O(1) using atomic operations.

---

## 4. Core Entities

Identified from functional requirements:

| Entity | Responsibility |
|--------|---------------|
| **UrlEntity** | Stores the mapping between short URL and original URL, tracks clicks, expiration, and creation date. |
| **SystemAnalytics** | Read-only snapshot of system-wide metrics (total links, clicks, active links). |
| **UrlEventType** | Enum representing events in the system (`CREATED`, `VISITED`). |

---

## 5. Relationships Between Entities

```
UrlShortenerService ──uses──▶ IUrlRepository (stores/retrieves UrlEntity)
UrlShortenerService ──uses──▶ IUrlGeneratorStrategy (generates short codes)
UrlShortenerService ──implements──▶ ISubject (notifies observers on events)
RealTimeAnalyticsTracker ──implements──▶ IObserver (reacts to URL events)
RealTimeAnalyticsTracker ──produces──▶ SystemAnalytics
UrlEntity ──contains──▶ UrlEventType (used in notifications)
```

---

## 6. Design Patterns Used

| Pattern | Where | Why |
|---------|-------|-----|
| **Strategy** | `IUrlGeneratorStrategy` / `CounterBasedBase62UrlGeneratorStrategy` | Allows swapping URL generation algorithms (counter-based, hash-based, random) without changing service code. |
| **Observer** | `ISubject` / `IObserver` / `RealTimeAnalyticsTracker` | Decouples analytics from core business logic. New observers can be added without modifying the service. |
| **Repository** | `IUrlRepository` / `InMemoryUrlRepository` | Abstracts data storage. Can swap in-memory for database without touching service logic. |

---

## 7. Class Diagram

```
┌─────────────────────────────┐
│      «interface»            │
│     IUrlGeneratorStrategy   │
├─────────────────────────────┤
│ + Generate(longUrl): string │
└──────────────┬──────────────┘
               │ implements
┌──────────────▼──────────────────────────┐
│ CounterBasedBase62UrlGeneratorStrategy   │
├─────────────────────────────────────────┤
│ - _allowedCharacters: string            │
│ - _counter: long                        │
├─────────────────────────────────────────┤
│ + Generate(longUrl): string             │
│ - EncodeBase62(id): string              │
└─────────────────────────────────────────┘

┌───────────────────────────────────────┐
│          «interface»                  │
│          IUrlRepository               │
├───────────────────────────────────────┤
│ + AddUrlEntity(entity)                │
│ + GetEntityByAlias(shortUrl): UrlEntity? │
│ + ShortUrlExists(alias): bool         │
│ + UpdateEntity(entity)                │
│ + DeleteEntity(entity)                │
└──────────────┬────────────────────────┘
               │ implements
┌──────────────▼────────────────────────┐
│      InMemoryUrlRepository            │
├───────────────────────────────────────┤
│ - _shortToEntityMap: Dictionary       │
└───────────────────────────────────────┘

┌────────────────────────────────────────────┐
│           «interface»                      │
│            ISubject                        │
├────────────────────────────────────────────┤
│ + Subscribe(observer)                      │
│ + Unsubscribe(observer)                    │
│ + Notify(eventType, entity)                │
└──────────────┬─────────────────────────────┘
               │ implements
┌──────────────▼─────────────────────────────┐
│        UrlShortenerService                 │
├────────────────────────────────────────────┤
│ - _repository: IUrlRepository              │
│ - _generatorStrategy: IUrlGeneratorStrategy│
│ - _observers: ImmutableHashSet<IObserver>  │
├────────────────────────────────────────────┤
│ + ShortenUrl(longUrl, customAlias?, ttl?)  │
│ + RedirectUrl(shortUrl): string            │
│ + Subscribe(observer)                      │
│ + Unsubscribe(observer)                    │
│ + Notify(eventType, entity)                │
└────────────────────────────────────────────┘

┌─────────────────────────────────────┐
│        «interface»                  │
│         IObserver                   │
├─────────────────────────────────────┤
│ + Update(eventType, entity)         │
└──────────────┬──────────────────────┘
               │ implements
┌──────────────▼──────────────────────┐
│    RealTimeAnalyticsTracker         │
├─────────────────────────────────────┤
│ - _totalLinks: int                  │
│ - _totalClicks: int                 │
│ - _expirations: ConcurrentDict     │
├─────────────────────────────────────┤
│ + Update(eventType, entity)         │
│ + GetAnalytics(): SystemAnalytics   │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│           UrlEntity                 │
├─────────────────────────────────────┤
│ + OriginalUrl: string               │
│ + ShortUrl: string                  │
│ + ExpirationTime: DateTimeOffset?   │
│ + CreatedDate: DateTimeOffset       │
│ + LastAccessed: DateTimeOffset?     │
│ + TotalClicks: int                  │
├─────────────────────────────────────┤
│ + RecordVisit()                     │
│ + IsExpired(): bool                 │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│  «record» SystemAnalytics           │
├─────────────────────────────────────┤
│ + TotalLinks: int                   │
│ + TotalClicks: int                  │
│ + ActiveLinks: int                  │
└─────────────────────────────────────┘

┌─────────────────────────┐
│  «enum» UrlEventType    │
├─────────────────────────┤
│ CREATED                 │
│ VISITED                 │
└─────────────────────────┘
```

---

## 8. Code for Each Class and Interface

### UrlEntity

```csharp
namespace URLShotenerV1.Entities;

public class UrlEntity
{
    public string OriginalUrl { get; }
    public string ShortUrl { get; }
    public DateTimeOffset? ExpirationTime { get; }
    public DateTimeOffset? LastAccessed { get; private set; }
    public DateTimeOffset CreatedDate { get; }

    private int _totalClicks;
    public int TotalClicks => _totalClicks;

    public UrlEntity(string originalUrl, string shortUrl, DateTimeOffset? expirationTime)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
            throw new ArgumentException("Original URL cannot be empty.");
        if (string.IsNullOrWhiteSpace(shortUrl))
            throw new ArgumentException("Short URL cannot be empty.");

        OriginalUrl = originalUrl;
        ShortUrl = shortUrl;
        ExpirationTime = expirationTime;
        CreatedDate = DateTimeOffset.UtcNow;
        _totalClicks = 0;
        LastAccessed = null;
    }

    public void RecordVisit()
    {
        _totalClicks++;
        LastAccessed = DateTimeOffset.UtcNow;
    }

    public bool IsExpired()
    {
        return ExpirationTime.HasValue && ExpirationTime.Value < DateTimeOffset.UtcNow;
    }
}
```

### SystemAnalytics

```csharp
namespace URLShotenerV1.Entities;

public record SystemAnalytics(int TotalLinks, int TotalClicks, int ActiveLinks);
```

### UrlEventType

```csharp
public enum UrlEventType
{
    CREATED,
    VISITED,
}
```

### IUrlGeneratorStrategy

```csharp
namespace URLShotenerV1.Strategies;

public interface IUrlGeneratorStrategy
{
    string Generate(string longUrl);
}
```

### CounterBasedBase62UrlGeneratorStrategy

```csharp
using System.Threading;
using System.Text;

namespace URLShotenerV1.Strategies;

public class CounterBasedBase62UrlGeneratorStrategy : IUrlGeneratorStrategy
{
    private const string _allowedCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private long _counter = 0;

    public string Generate(string longUrl)
    {
        long id = Interlocked.Increment(ref _counter);
        return EncodeBase62(id);
    }

    private string EncodeBase62(long id)
    {
        if (id == 0)
            return _allowedCharacters[0].ToString();

        var shortUrl = new StringBuilder();

        while (id > 0)
        {
            int remainder = (int)(id % 62);
            shortUrl.Insert(0, _allowedCharacters[remainder]);
            id /= 62;
        }

        return shortUrl.ToString();
    }
}
```

### IUrlRepository

```csharp
using URLShotenerV1.Entities;

namespace URLShotenerV1.Repository;

public interface IUrlRepository
{
    void AddUrlEntity(UrlEntity entity);
    bool ShortUrlExists(string alias);
    UrlEntity? GetEntityByAlias(string shortUrl);
    void UpdateEntity(UrlEntity urlEntity);
    void DeleteEntity(UrlEntity urlEntity);
}
```

### InMemoryUrlRepository

```csharp
using URLShotenerV1.Entities;
using URLShotenerV1.Exceptions;

namespace URLShotenerV1.Repository;

public class InMemoryUrlRepository : IUrlRepository
{
    private readonly Dictionary<string, UrlEntity> _shortToEntityMap;

    public InMemoryUrlRepository()
    {
        _shortToEntityMap = new Dictionary<string, UrlEntity>();
    }

    public void AddUrlEntity(UrlEntity entity)
    {
        if (_shortToEntityMap.TryAdd(entity.ShortUrl, entity) == false)
        {
            throw new AliasAlreadyTakenException(entity.ShortUrl);
        }
    }

    public bool ShortUrlExists(string alias)
    {
        return _shortToEntityMap.ContainsKey(alias);
    }

    public UrlEntity? GetEntityByAlias(string shortUrl)
    {
        _shortToEntityMap.TryGetValue(shortUrl, out UrlEntity? url);
        return url;
    }

    public void UpdateEntity(UrlEntity urlEntity)
    {
        _shortToEntityMap[urlEntity.ShortUrl] = urlEntity;
    }

    public void DeleteEntity(UrlEntity urlEntity)
    {
        _shortToEntityMap?.Remove(urlEntity.ShortUrl);
    }
}
```

### IObserver

```csharp
using URLShotenerV1.Entities;

namespace URLShotenerV1.Observers;

public interface IObserver
{
    void Update(UrlEventType eventType, UrlEntity entity);
}
```

### ISubject

```csharp
using URLShotenerV1.Entities;

namespace URLShotenerV1.Observers;

public interface ISubject
{
    void Subscribe(IObserver observer);
    void Unsubscribe(IObserver observer);
    void Notify(UrlEventType eventType, UrlEntity entity);
}
```

### RealTimeAnalyticsTracker

```csharp
using System.Collections.Concurrent;
using URLShotenerV1.Entities;

namespace URLShotenerV1.Observers;

public class RealTimeAnalyticsTracker : IObserver
{
    private int _totalLinks;
    private int _totalClicks;
    private readonly ConcurrentDictionary<string, DateTimeOffset?> _expirations;

    public RealTimeAnalyticsTracker()
    {
        _expirations = new();
    }

    public void Update(UrlEventType eventType, UrlEntity entity)
    {
        switch (eventType)
        {
            case UrlEventType.CREATED:
                Interlocked.Increment(ref _totalLinks);
                _expirations.TryAdd(entity.ShortUrl, entity.ExpirationTime);
                break;
            case UrlEventType.VISITED:
                Interlocked.Increment(ref _totalClicks);
                break;
        }
    }

    public SystemAnalytics GetAnalytics()
    {
        int active = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var exp in _expirations.Values)
        {
            if (!exp.HasValue || exp.Value > now)
            {
                active++;
            }
        }

        return new SystemAnalytics(_totalLinks, _totalClicks, active);
    }
}
```

### UrlShortenerService

```csharp
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

    public string ShortenUrl(string longUrl, string? customAlias, DateTimeOffset? expirationTime = null)
    {
        string aliasToUse;
        if (!string.IsNullOrEmpty(customAlias))
        {
            if (_repository.ShortUrlExists(customAlias))
                throw new AliasAlreadyTakenException(customAlias);
            aliasToUse = customAlias;
        }
        else
        {
            int maxRetries = 5;
            do
            {
                aliasToUse = _generatorStrategy.Generate(longUrl);
                maxRetries--;
                if (maxRetries == 0)
                    throw new Exception("Failed to generate a unique alias.");
            } while (_repository.ShortUrlExists(aliasToUse) == true);
        }

        var urlEntry = new UrlEntity(originalUrl: longUrl, shortUrl: aliasToUse, expirationTime: expirationTime);
        _repository.AddUrlEntity(urlEntry);
        Notify(UrlEventType.CREATED, urlEntry);

        return aliasToUse;
    }

    public string RedirectUrl(string shortUrl)
    {
        if (string.IsNullOrEmpty(shortUrl))
            throw new InvalidShortUrlException(shortUrl);

        var entity = _repository.GetEntityByAlias(shortUrl);
        if (entity == null)
            throw new UrlNotFoundException(shortUrl);
        if (entity.IsExpired())
        {
            _repository.DeleteEntity(entity);
            throw new UrlExpiredException(shortUrl);
        }

        entity.RecordVisit();
        _repository.UpdateEntity(entity);
        Notify(UrlEventType.VISITED, entity);

        return entity.OriginalUrl;
    }

    public void Subscribe(IObserver observer)
    {
        ImmutableInterlocked.Update(ref _observers, list => list.Add(observer));
    }

    public void Unsubscribe(IObserver observer)
    {
        ImmutableInterlocked.Update(ref _observers, list => list.Remove(observer));
    }

    public void Notify(UrlEventType eventType, UrlEntity entity)
    {
        foreach (var observer in _observers)
        {
            observer.Update(eventType, entity);
        }
    }
}
```

### Exceptions

```csharp
// AliasAlreadyTakenException.cs
namespace URLShotenerV1.Exceptions;

public class AliasAlreadyTakenException : Exception
{
    public AliasAlreadyTakenException(string alias)
        : base($"The custom alias : {alias} is already in use.") { }
}

// InvalidShortUrlException.cs
namespace URLShotenerV1.Exceptions;

public class InvalidShortUrlException : Exception
{
    public InvalidShortUrlException(string shortUrl)
        : base($"{shortUrl} is invalid") { }
}

// UrlExpiredException.cs
namespace URLShotenerV1.Exceptions;

public class UrlExpiredException : Exception
{
    public UrlExpiredException(string shortUrl)
        : base($"{shortUrl} has expired.") { }
}

// UrlNotFoundException.cs
namespace URLShotenerV1.Exceptions;

public class UrlNotFoundException : Exception
{
    public UrlNotFoundException(string shortUrl)
        : base($"{shortUrl} not found in database") { }
}
```

---

## 9. Evolution to V2

V2 evolves V1 by making the system **async-ready** and **fully thread-safe**:

| Aspect | V1 | V2 |
|--------|----|----|
| **Service methods** | Synchronous (`string ShortenUrl(...)`) | Async (`Task<string> ShortenUrlAsync(...)`) |
| **Repository interface** | Sync (`void AddUrlEntity(...)`) | Async (`Task AddUrlEntityAsync(...)`) |
| **Repository storage** | `Dictionary<string, UrlEntity>` | `ConcurrentDictionary<string, UrlEntity>` |
| **RecordVisit()** | `_totalClicks++` (not thread-safe) | `Interlocked.Increment(ref _totalClicks)` |

### Why these changes matter:

1. **Async** — In production, repository calls hit a database or network. Async prevents thread pool starvation under load.
2. **ConcurrentDictionary** — Multiple threads can safely read/write URL mappings simultaneously without locks.
3. **Interlocked.Increment** — Ensures click counts are accurate even under concurrent access (V1's `++` can lose increments).