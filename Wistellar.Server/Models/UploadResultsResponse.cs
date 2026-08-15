namespace Wistellar.Server.Models
{
    public class UploadResultsResponse
    {
        public string TimeTaken { get; set; } = "";
        public long Filesize { get; set; }
        public string Filename { get; set; } = "";
        public List<TransidResponse> Transids { get; set; } = [];
    }
}
