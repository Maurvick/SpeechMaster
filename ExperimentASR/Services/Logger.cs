using System.IO;
using System.Text;

namespace SpeechMaster.Services
{
	public class Logger
	{
		private readonly string _filePath;
		private readonly object _lock = new object();
		private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB Limit

		// FIXME: This is writes multiple times to the log file.
		public Logger()
		{
			_filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs.txt");

			// Check file size on startup and rotate if necessary
			ManageLogFileRotation();

			// Add a separator for the new session
			LogRaw("\n--------------------------------------------------");
			LogRaw($"SESSION STARTED: {DateTime.Now:F}");
			LogRaw("--------------------------------------------------");
		}

		public void LogInfo(string message)
		{
			WriteLog("INFO", message);
		}

		public void LogError(string message)
		{
			WriteLog("ERROR", message);
		}

		public void LogError(string message, Exception ex)
		{
			WriteLog("ERROR", $"{message} | {ex.GetType().Name}: {ex.Message}\nStack Trace: {ex.StackTrace}");
		}

		private void WriteLog(string level, string message)
		{
			var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
			LogRaw(logEntry);
		}

		private void LogRaw(string line)
		{
			lock (_lock)
			{
				try
				{
					// The 'true' argument here enables APPEND mode (Keep History)
					using (StreamWriter writer = new StreamWriter(_filePath, true, Encoding.UTF8))
					{
						writer.WriteLine(line);
					}
				}
				catch
				{
					// Ignore write errors to avoid crashing the app
				}
			}
		}

		private void ManageLogFileRotation()
		{
			lock (_lock)
			{
				try
				{
					FileInfo fi = new FileInfo(_filePath);
					if (fi.Exists && fi.Length > MaxFileSizeBytes)
					{
						// File is too big. Rename it to logs_backup_YYYYMMDD_HHmm.txt
						string backupName = $"logs_backup_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
						string backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, backupName);

						fi.MoveTo(backupPath); // Rename current log to backup

						// After moving, the next WriteLog will automatically create a fresh logs.txt
					}
				}
				catch
				{
					// If rotation fails (e.g., file in use), we just keep appending to the big file
				}
			}
		}
	}
}
