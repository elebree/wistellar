namespace Wistellar.Core.Services.Vendor
{
    public interface IVendorResolverService
    {
        string? Get(string mac);
    }
}