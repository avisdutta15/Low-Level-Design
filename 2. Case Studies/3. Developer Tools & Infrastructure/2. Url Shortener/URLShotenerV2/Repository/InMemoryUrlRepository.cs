using System.Collections.Concurrent;
using URLShotenerV2.Entities;
using URLShotenerV2.Exceptions;

namespace URLShotenerV2.Repository;

public class InMemoryUrlRepository : IUrlRepository
{
    private readonly ConcurrentDictionary<string, UrlEntity> _shortToEntityMap;

    public InMemoryUrlRepository()
    {
        _shortToEntityMap = new ConcurrentDictionary<string, UrlEntity>();
    }

    public Task AddUrlEntityAsync(UrlEntity entity)
    {
        if (_shortToEntityMap.TryAdd(entity.ShortUrl, entity) == false)
        {
            throw new AliasAlreadyTakenException(entity.ShortUrl);
        }
        return Task.CompletedTask;
    }

    public Task<bool> AliasExistsAsync(string alias)
    {
        return Task.FromResult(_shortToEntityMap.ContainsKey(alias));
    }
    public Task<UrlEntity?> GetEntityByAliasAsync(string shortUrl)
    {
        _shortToEntityMap.TryGetValue(shortUrl, out UrlEntity? entity);
        return Task.FromResult(entity);
    }

    public Task UpdateEntityAsync(UrlEntity entity)
    {
        _shortToEntityMap[entity.ShortUrl] = entity;
        return Task.CompletedTask;
    }
}
