using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ExperimentASR.Models;
using ExperimentASR.Services.Engines;

namespace ExperimentASR.Services
{
	/// <summary>
	/// Manages ASR engines within the application.
	/// Provides information about available engines 
	/// and handles their initialization and configuration.
	/// </summary>
	public class EngineManager
    {
		// TODO: Duplicate of EngineSetupService?
		private List<AsrEngine> _engines = new();

		public List<AsrEngine> AvailableEngines
		{
			get { return _engines = new(); }
			protected set { _engines = value; }
		}


		private string AsrEngineFolder = "SpeechRecognition";

		private bool isWhisperExists = false;
		private bool isVoskExists = false;
		private bool isSileroExists = false;

		public EngineManager()
		{
			if (!Directory.Exists(AsrEngineFolder))
			{
				Directory.CreateDirectory(AsrEngineFolder);
			}

			_engines.AddRange(
			[
				new WhisperEngine(),
			]);
		}

		public void CheckAvailableEngines() 
		{
			if (Directory.Exists(Path.Combine(AsrEngineFolder, "whisper-bin-x64"))) 
			{ 

			}
			else
			{

			}
		}
	}
}
