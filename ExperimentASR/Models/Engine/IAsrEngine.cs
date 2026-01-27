using System;
using System.Collections.Generic;
using System.Text;
using SpeechMaster.Models.Transcription;

namespace SpeechMaster.Models
{
    public interface IAsrEngine
    {
		string Name { get; }
		bool SupportsGpu { get; }
		Task<TranscriptionResult> TranscribeAsync(string audioPath);
	}
}
