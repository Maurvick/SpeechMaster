using NAudio.Wave;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using System.IO;

namespace ExperimentASR.Services
{
    public class ParquetDatasetLoader
    {
        public class Sample
        {
            public string AudioPath { get; set; }     // шлях до збереженого WAV
            public string ReferenceText { get; set; }
            public float Duration { get; set; }
            public int SampleRate { get; set; }
        }

        public async Task<List<Sample>> LoadFromParquetAsync(string parquetPath, string outputFolder = "parquet_extracted")
        {
            var samples = new List<Sample>();
            Directory.CreateDirectory(outputFolder);

            // Асинхронне відкриття
            using var reader = await ParquetReader.CreateAsync(parquetPath);

            // Шукаємо колонки
            var schema = reader.Schema;
            DataField? audioField = schema.GetDataFields().FirstOrDefault(f => f.Name == "audio");
            DataField? textField = schema.GetDataFields().FirstOrDefault(f => f.Name == "transcription");
            DataField? durationField = schema.GetDataFields().FirstOrDefault(f => f.Name == "duration");

            if (audioField == null || textField == null)
                throw new InvalidOperationException("Колонки 'audio' або 'transcription' не знайдено в Parquet");

            for (int i = 0; i < reader.RowGroupCount; i++)
            {
                using var rowGroup = reader.OpenRowGroupReader(i);

                // ВИПРАВЛЕНО: Await для Task<DataColumn>
                var audioColumnTask = rowGroup.ReadColumnAsync(audioField);
                var textColumnTask = rowGroup.ReadColumnAsync(textField);
                var durationColumnTask = durationField != null ? rowGroup.ReadColumnAsync(durationField) : null;

                var audioColumn = await audioColumnTask;
                var textColumn = await textColumnTask;
                var durationColumn = durationColumnTask != null ? await durationColumnTask : null;

                // ВИПРАВЛЕНО: Data — тепер доступне після await
                var audioData = audioColumn.Data as object[]; // масив об'єктів (структури audio)
                var textData = textColumn.Data as string[];
                var durationData = durationColumn?.Data as float[];

                for (int j = 0; j < rowGroup.RowCount; j++)
                {
                    var audioObj = audioData[j];
                    if (audioObj is Dictionary<string, object> audioDict) // ВИПРАВЛЕНО: Dictionary замість DataStruct
                    {
                        // Доступ до полів: audio.array (float32 bytes), audio.sampling_rate
                        if (audioDict.TryGetValue("array", out var audioArrayObj) && audioArrayObj is byte[] audioBytes &&
                            audioDict.TryGetValue("sampling_rate", out var srObj) && srObj is long samplingRateLong)
                        {
                            var samplingRate = (int)samplingRateLong;
                            var referenceText = textData[j] ?? "";
                            var duration = durationData?[j] ?? (audioBytes.Length / (samplingRate * 4f)); // float32 = 4 байти на семпл

                            // Зберігаємо як WAV
                            string wavPath = Path.Combine(outputFolder, $"sample_{samples.Count:D5}.wav");
                            ConvertBytesToWav(audioBytes, samplingRate, wavPath);

                            samples.Add(new Sample
                            {
                                AudioPath = wavPath,
                                ReferenceText = referenceText,
                                Duration = duration,
                                SampleRate = samplingRate
                            });
                        }
                    }
                }
            }

            return samples;
        }

        // ВИПРАВЛЕНО: Використовуємо WaveFormatConversionStream замість SampleRateConverter
        private void ConvertBytesToWav(byte[] pcmBytes, int sampleRate, string outputPath)
        {
            bool isFloat32 = pcmBytes.Length % 4 == 0;

            using var ms = new MemoryStream();
            using var writer = new WaveFileWriter(ms, new WaveFormat(sampleRate, 16, 1));

            if (isFloat32)
            {
                for (int i = 0; i < pcmBytes.Length; i += 4)
                {
                    float sample = BitConverter.ToSingle(pcmBytes, i);
                    sample = Math.Clamp(sample, -1f, 1f);
                    writer.WriteSample(sample); // WriteSample приймає float від -1 до 1
                }
            }
            else // int16
            {
                for (int i = 0; i < pcmBytes.Length; i += 2)
                {
                    if (i + 1 < pcmBytes.Length)
                    {
                        short sample = BitConverter.ToInt16(pcmBytes, i);
                        writer.WriteSample(sample / 32768f);
                    }
                }
            }

            File.WriteAllBytes(outputPath, ms.ToArray());
        }
    }
}
