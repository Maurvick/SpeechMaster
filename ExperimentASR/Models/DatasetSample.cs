namespace ExperimentASR.Models
{
    public class DatasetSample
    {
        public int Id { get; set; }
        public string AudioPath { get; set; }
        public string Transcription { get; set; }
        public string TranscriptionStressed { get; set; }
        public double Duration { get; set; }
        public int SampleRate { get; set; }
    }
}
