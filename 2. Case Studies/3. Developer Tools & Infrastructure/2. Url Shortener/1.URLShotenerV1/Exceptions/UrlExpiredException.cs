namespace _1.URLShotenerV1.Exceptions;

public class UrlExpiredException : Exception
{
    public UrlExpiredException(string shortUrl) : base($"{shortUrl} has expired.") 
    { }
}
