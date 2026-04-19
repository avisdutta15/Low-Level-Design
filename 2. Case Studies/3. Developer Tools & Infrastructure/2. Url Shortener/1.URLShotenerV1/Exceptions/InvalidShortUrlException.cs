namespace _1.URLShotenerV1.Exceptions;

public class InvalidShortUrlException : Exception
{
    public InvalidShortUrlException(string shortUrl) :
        base($"{shortUrl} is invalid")
    { }
}
