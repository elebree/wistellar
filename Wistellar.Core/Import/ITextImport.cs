using Wistellar.Core.Models;

namespace Wistellar.Core.Import
{
    public interface ITextImport
    {
        string Name { get; }
        bool Detect(string contentType, string header);
        Task<IEnumerable<Observation>> Import(string header, TextReader reader);
    }
}
