namespace _1.URLShotenerV1.Exceptions;

public class UrlNotFoundException : Exception
{
    public UrlNotFoundException(string shortUrl) : base($"{shortUrl} not found in database")
    { }
}
