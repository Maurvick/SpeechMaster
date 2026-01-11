namespace ExperimentASR.Models
{
    public class TranscriptionResult
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public string? Transcript { get; set; }

		// This is crucial for fixing timestamps
		public List<Segment> Segments { get; set; } = new List<Segment>();
	}

	public class Segment
	{
		public double Start { get; set; }
		public double End { get; set; }
		public string Text { get; set; }
	}
}
