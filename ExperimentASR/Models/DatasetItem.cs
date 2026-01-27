namespace SpeechMaster.Models
{
    public class DatasetItem
	{
        public string AudioPath { get; set; }
        public string ReferenceText { get; set; }
        public double Duration { get; set; } // in seconds
		public int SampleRate { get; set; }
	}
}
