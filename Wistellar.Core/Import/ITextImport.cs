using Wistellar.Core.Models;

namespace Wistellar.Core.Import
{
    public interface ITextImport
    {
        string Name { get; }
        bool Detect(string contentType, string header);
        /// <summary>Reads all observations asynchronously; the result must not read from <paramref name="reader"/>.</summary>
        Task<IEnumerable<Observation>> Import(string header, TextReader reader, CancellationToken ct = default);
    }
}
