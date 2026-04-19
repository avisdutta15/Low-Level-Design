namespace _1.URLShotenerV1.Exceptions;

public class AliasAlreadyTakenException : Exception
{
    public AliasAlreadyTakenException(string alias) 
        : base($"The custom alias : {alias} is already in use.")
    {
    }
}