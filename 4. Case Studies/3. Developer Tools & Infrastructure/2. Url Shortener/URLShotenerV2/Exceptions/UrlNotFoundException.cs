namespace URLShotenerV2.Exceptions;

public class UrlNotFoundException : Exception
{
    public UrlNotFoundException(string alias) : base($"The alias '{alias}' does not exist.") { }
}
