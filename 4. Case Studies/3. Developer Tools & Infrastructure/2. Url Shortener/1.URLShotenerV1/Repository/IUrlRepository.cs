using _1.URLShotenerV1.Entities;

namespace _1.URLShotenerV1.Repository;

public interface IUrlRepository
{
    void AddUrlEntity(UrlEntity entity);
    bool AliasExists(string alias);
    UrlEntity? GetEntityByAlias(string shortUrl);
    void UpdateEntity(UrlEntity urlEntity);
}
