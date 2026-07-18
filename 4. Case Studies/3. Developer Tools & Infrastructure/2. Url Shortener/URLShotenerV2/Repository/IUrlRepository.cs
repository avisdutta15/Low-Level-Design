using URLShotenerV2.Entities;

namespace URLShotenerV2.Repository;

public interface IUrlRepository
{
    Task AddUrlEntityAsync(UrlEntity entity);
    Task<UrlEntity?> GetEntityByAliasAsync(string shortUrl);
    Task<bool> ShortUrlExistsAsync(string alias);
    Task UpdateEntityAsync(UrlEntity urlEntity);
    Task DeleteEntityAsync(UrlEntity urlEntity);
}