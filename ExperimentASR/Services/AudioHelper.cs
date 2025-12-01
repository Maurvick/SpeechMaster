using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Text;

namespace ExperimentASR.Models
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
    }
}
