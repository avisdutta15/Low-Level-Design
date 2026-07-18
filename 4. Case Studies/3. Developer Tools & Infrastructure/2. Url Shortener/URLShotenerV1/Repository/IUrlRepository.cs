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
