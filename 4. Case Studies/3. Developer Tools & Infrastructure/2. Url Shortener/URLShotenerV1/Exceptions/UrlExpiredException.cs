namespace URLShotenerV1.Exceptions;

public class UrlExpiredException : Exception
{
    public UrlExpiredException(string shortUrl) : base($"{shortUrl} has expired.") 
    { }
}
