using NAudio.Wave;

namespace SpeechMaster.Services
{
    public class AudioHelper
    {
        public static int GetAudioFileDuration(string filePath)
        {
            // Synchronous read so callers get the actual duration
            using var reader = new AudioFileReader(filePath);
            TimeSpan duration = reader.TotalTime;
            return (int)duration.TotalSeconds;
        }

		// FIXME: Extract audio from video files

	}
}
