using Wistellar.Core.Models;

namespace Wistellar.Core.Services.Vendor
{
    public interface IOuiSourceService
    {
        IAsyncEnumerable<OuiVendorInfo> Get();
    }
}