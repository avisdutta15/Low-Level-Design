namespace URLShotenerV2.Exceptions;

public class UrlExpiredException : Exception
{
    public UrlExpiredException(string alias) : base($"The alias '{alias}' has expired.") { }
}
