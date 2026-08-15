using Wistellar.Core.Entities;

namespace Wistellar.Core.Services.MobileNetwork
{
    public interface IMccMncService
    {
        IAsyncEnumerable<MccMnc> Get();
    }
}