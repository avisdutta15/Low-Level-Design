namespace URLShotenerV2.Exceptions;

public class InvalidShortUrlException : Exception
{
    public InvalidShortUrlException(string shortUrl) :
        base($"{shortUrl} is invalid")
    { }
}
