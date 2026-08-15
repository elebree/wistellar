namespace Wistellar.Core.Models
{
    /// <summary>
    /// Outcome of importing one file out of an upload. A single upload can produce several of
    /// these when it is an archive holding multiple logs.
    /// </summary>
    public class ImportResult
    {
        public long TimeTaken { get; set; }
        public long FileSize { get; set; }
        public string FileName { get; set; } = "";
        public bool Success { get; set; }
        public string Warning { get; set; } = "";
    }
}
