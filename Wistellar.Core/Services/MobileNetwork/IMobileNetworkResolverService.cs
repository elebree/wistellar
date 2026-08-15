namespace Wistellar.Core.Services.MobileNetwork
{
    public interface IMobileNetworkResolverService
    {
        string? Get(string mac);
    }
}