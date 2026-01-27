namespace SpeechMaster.Models.Transcription
{
	public class TranscriptionHistoryItem
	{
		public string FileName { get; set; }
		public string FullPath { get; set; }
		public string TranscriptText { get; set; }
		public DateTime ProcessedDate { get; set; }
		public string DisplayDate => ProcessedDate.ToString("HH:mm:ss");
	}
}
