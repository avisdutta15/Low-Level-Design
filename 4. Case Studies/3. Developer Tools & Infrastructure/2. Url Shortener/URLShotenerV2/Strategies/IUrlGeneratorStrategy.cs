namespace URLShotenerV2.Strategies;

public interface IUrlGeneratorStrategy
{
    string Generate(string longUrl);
}
