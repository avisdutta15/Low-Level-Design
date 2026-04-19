namespace URLShotenerV2.Strategies;

public class CounterBasedBase62UrlGeneratorStrategy : IUrlGeneratorStrategy
{
    private const string _allowedCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private long _counter = 1;

    public string Generate(string longUrl)
    {
        // Atomically increment the counter for thread safety
        long id = Interlocked.Increment(ref _counter);
        return EncodeBase62(id);
    }

    private string EncodeBase62(long id)
    {
        if (id == 0) return _allowedCharacters[0].ToString();

        var shortUrl = new System.Text.StringBuilder();

        while (id > 0)
        {
            int remainder = (int)(id % 62);
            shortUrl.Insert(0, _allowedCharacters[remainder]);
            id /= 62;
        }

        return shortUrl.ToString();
    }
}
