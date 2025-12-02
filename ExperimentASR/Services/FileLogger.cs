using System;
using System.IO;

namespace ExperimentASR.Services
{
    /// <summary>
    /// Provides simple logging functionality by writing informational messages to a text file.
    /// Thread-safe: concurrent calls to LogInfo are serialized using a static lock.
    /// </summary>
    public sealed class FileLogger
    {
        private const string LogsFilePath = "logs.txt";
        private static readonly object s_lock = new();

        public FileLogger()
        {
            // Ensure file exists in a thread-safe way and dispose the stream immediately.
            lock (s_lock)
            {
                if (!File.Exists(LogsFilePath))
                {
                    using var fs = new FileStream(LogsFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                }
            }
        }

        public void LogInfo(string message)
        {
            var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}{Environment.NewLine}";

            lock (s_lock)
            {
                // Open/append/close under the same lock to avoid interleaved writes.
                File.AppendAllText(LogsFilePath, entry);
            }
        }
    }
}
