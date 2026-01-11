using ExperimentASR.Models;
using System.IO;
using System.Text.Json;
using Vosk;

namespace ExperimentASR.Services.Engines
{
    public class VoskEngine: AsrEngine, IDisposable
    {
        private readonly Model _model;
        private VoskRecognizer? _recognizer;
        // Vosk supports only 16 kGhz
        private readonly float _sampleRate = 16000f;

        public VoskEngine(string modelPath = "SpeechRecognition/vosk-model-small-uk-v3-nano")
        {
            Name = "Vosk UA (small) — CPU only";
            SupportsGpu = false;

            if (!Directory.Exists(modelPath))
                throw new DirectoryNotFoundException($"Vosk model not found: {modelPath}");

            _model = new Model(modelPath);
        }

        public void Dispose()
        {
            _recognizer?.Dispose();
            _model?.Dispose();
        }

        private byte[] ConvertToPcm16kHz(string inputPath)
        {
            using var reader = new NAudio.Wave.AudioFileReader(inputPath);
            var resampler = new NAudio.Wave.MediaFoundationResampler(reader, 16000);
            resampler.ResamplerQuality = 60; // висока якість

            var wholeFile = new List<byte>();
            var buffer = new byte[16384];
            int bytesRead;

            while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
            {
                // Переводимо float32 → int16 (Vosk приймає short[])
                for (int i = 0; i < bytesRead; i += 4)
                {
                    if (i + 3 < bytesRead)
                    {
                        float sample = BitConverter.ToSingle(buffer, i);
                        short shortSample = (short)(sample * 32767f);
                        byte[] shortBytes = BitConverter.GetBytes(shortSample);
                        wholeFile.Add(shortBytes[0]);
                        wholeFile.Add(shortBytes[1]);
                    }
                }
            }

            return wholeFile.ToArray();
        }

        public override async Task<string> TranscribeAsync(string audioPath)
        {
            return await Task.Run(() =>
            {
                // Конвертуємо будь-яке аудіо в 16kHz mono PCM (Vosk вимагає саме це)
                var pcmData = ConvertToPcm16kHz(audioPath);

                _recognizer ??= new VoskRecognizer(_model, _sampleRate);
                _recognizer.SetMaxAlternatives(0);
                _recognizer.AcceptWaveform(pcmData, pcmData.Length);

                var resultJson = _recognizer.Result();
                _recognizer.Reset();

                // Парсимо JSON відповідь Vosk
                try
                {
                    var jsonDoc = JsonDocument.Parse(resultJson);
                    return jsonDoc.RootElement
                                  .GetProperty("text")
                                  .GetString() ?? "";
                }
                catch
                {
                    return "";
                }
            });
        }
    }
}
