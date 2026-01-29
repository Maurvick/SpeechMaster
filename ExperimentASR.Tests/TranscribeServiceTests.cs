using Xunit;
using FluentAssertions; // Use NuGet: FluentAssertions
using SpeechMaster.Services;
using SpeechMaster.Models.Transcription;
using System.Text.Json;
using System.IO;

namespace SpeechMaster.Tests
{
	public class TranscribeServiceTests
	{
		private readonly TranscribeService _service;

		public TranscribeServiceTests()
		{
			// Note: This relies on SettingsManager working in the test env.
			// If SettingsManager fails, you might need to mock it or create a dummy settings file.
			_service = new TranscribeService();
		}

		// ---------------------------------------------------------
		// 1. VALIDATION TESTS
		// ---------------------------------------------------------

		[Fact]
		public void Transcribe_ShouldThrowException_WhenFileDoesNotExist()
		{
			// Arrange
			string fakePath = "C:\\NonExistentFile.wav";

			// Act
			Action act = () => _service.Transcribe(fakePath, WhisperModelType.Base);

			// Assert
			act.Should().Throw<FileNotFoundException>()
			   .WithMessage("*Audio file not found*");
		}

		// ---------------------------------------------------------
		// 2. PARSING LOGIC TESTS (The most important part)
		// ---------------------------------------------------------

		[Fact]
		public void ParseWhisperCppJson_ShouldParseValidJson_Correctly()
		{
			// Arrange
			string json = @"
            {
                ""transcription"": [
                    { ""from"": 0.0, ""to"": 2.5, ""text"": ""Hello world"" },
                    { ""from"": 2.5, ""to"": 5.0, ""text"": "" This is a test"" }
                ]
            }";

			// Act
			var result = _service.ParseWhisperCppJson(json);

			// Assert
			result.Status.Should().Be("success");
			result.Segments.Should().HaveCount(2);
			result.Text.Should().Be("Hello world This is a test");

			// Check specific segment details
			result.Segments[0].Start.Should().Be(0.0);
			result.Segments[0].End.Should().Be(2.5);
			result.Segments[0].Text.Should().Be("Hello world");
		}

		[Fact]
		public void ParseWhisperCppJson_ShouldHandleStringTimestamps()
		{
			// Whisper sometimes returns strings "00:00:01,500" or "1.5"
			// Arrange
			string json = @"
            {
                ""transcription"": [
                    { ""from"": ""00:00:01.500"", ""to"": ""00:00:03.000"", ""text"": ""String Time"" }
                ]
            }";

			// Act
			var result = _service.ParseWhisperCppJson(json);

			// Assert
			result.Segments.Should().HaveCount(1);
			result.Segments[0].Start.Should().Be(1.5);
			result.Segments[0].End.Should().Be(3.0);
		}

		[Fact]
		public void ParseWhisperCppJson_ShouldHandleMalformedJson_Gracefully()
		{
			// Arrange
			string badJson = "{ \"transcription\": [ ... BROKEN JSON ... }";

			// Act
			var result = _service.ParseWhisperCppJson(badJson);

			// Assert
			result.Status.Should().Be("error");
			result.Message.Should().Contain("JSON Parsing failed");
		}

		[Fact]
		public void ParseWhisperCppJson_ShouldHandleEmptyTranscription()
		{
			// Arrange
			string json = "{ \"transcription\": [] }";

			// Act
			var result = _service.ParseWhisperCppJson(json);

			// Assert
			result.Status.Should().Be("success");
			result.Segments.Should().BeEmpty();
			result.Text.Should().BeEmpty();
		}

		// ---------------------------------------------------------
		// 3. TIME EXTRACTION LOGIC
		// ---------------------------------------------------------

		[Theory]
		[InlineData("1.5", 1.5)]
		[InlineData("0", 0.0)]
		[InlineData("00:00:10", 10.0)] // TimeSpan parsing
		public void ExtractTime_ShouldParseVariousFormats(string input, double expected)
		{
			// Arrange
			// We need to construct a JsonElement to pass to the method.
			// This is a bit hacky but works for unit testing logic.
			string json = $"{{ \"time\": \"{input}\" }}";
			using (JsonDocument doc = JsonDocument.Parse(json))
			{
				var root = doc.RootElement;

				// Act
				double result = _service.ExtractTime(root, "time");

				// Assert
				result.Should().Be(expected);
			}
		}

		[Fact]
		public void ExtractTime_ShouldParseNumericJson()
		{
			// Arrange
			string json = "{ \"time\": 42.5 }";
			using (JsonDocument doc = JsonDocument.Parse(json))
			{
				var root = doc.RootElement;
				double result = _service.ExtractTime(root, "time");
				result.Should().Be(42.5);
			}
		}
	}
}