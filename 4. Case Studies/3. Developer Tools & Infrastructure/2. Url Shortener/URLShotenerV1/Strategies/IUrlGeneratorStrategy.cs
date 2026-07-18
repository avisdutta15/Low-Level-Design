namespace URLShotenerV1.Strategies;

public interface IUrlGeneratorStrategy
{
    string Generate(string longUrl);
}
