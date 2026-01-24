namespace SpeechMaster.Models
{
    public abstract class AsrEngine
    {
        public abstract Task<string> TranscribeAsync(string audioPath);
        public string Name { get; protected set; }
        public bool SupportsGpu { get; protected set; }
    }
}
