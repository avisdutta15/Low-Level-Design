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
        if(_shortToEntityMap.TryAdd(entity.ShortUrl, entity) == false)
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
