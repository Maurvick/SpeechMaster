namespace SpeechMaster.Services
{
    public class StatusService
    {
		// Singleton instance
		private static StatusService? _instance;
		public static StatusService Instance => _instance ??= new StatusService();
		// Subscribe to this event to get status updates
		public event Action<string>? OnStatusChanged;
		public event Action<double>? OnProgressChanged;

		private StatusService() { }

		public void UpdateStatus(string message)
		{
			// Invoke the event to notify subscribers
			OnStatusChanged?.Invoke(message);
		}

		public void ResetProgress()
		{
			OnProgressChanged?.Invoke(Double.MinValue);
		}

		public void SetProgress(double value)
		{
			OnProgressChanged?.Invoke(value);
		}
	}
}
