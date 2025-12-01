namespace ExperimentASR.Models
{
    public class TranscriptionHistory
    {
        public string? FileName { get; set; }
        public string? Transcript { get; set; }
        public string? Date { get; set; }
        public double? Accuracy { get; set; }
    }
}
