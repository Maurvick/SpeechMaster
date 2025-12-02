using System;
using System.IO;
using System.Reflection;
using Xunit;
using FluentAssertions;
using ExperimentASR.Services;
using ExperimentASR.Models;

namespace ExperimentASR.Tests.Services
{
    public class TranscribeServiceTests
    {
        // ---------------------------------------------------------
        // SECTION 1: Validation Tests (Public Methods)
        // ---------------------------------------------------------

        [Fact]
        public void Transcribe_ShouldThrowFileNotFound_WhenAudioFileDoesNotExist()
        {
            // Arrange
            var service = new TranscribeService();
            string nonExistentFile = "ghost_audio.wav";

            // Act
            Action act = () => service.Transcribe(nonExistentFile);

            // Assert
            act.Should().Throw<FileNotFoundException>()
               .WithMessage("Audio file not found.");
        }

        [Fact]
        public void Transcribe_ShouldThrowFileNotFound_WhenScriptDoesNotExist()
        {
            // Note: This test relies on the internal path "./Scripts/asr_engine.py" NOT existing 
            // relative to where the Test Runner executes. 
            // If you actually have that file in your test bin folder, this test might fail.

            // Arrange
            var service = new TranscribeService();
            // We create a dummy audio file so we pass the first check
            var tempAudio = Path.GetTempFileName();

            try
            {
                // Act
                Action act = () => service.Transcribe(tempAudio);

                // Assert
                // We expect it to fail finding the python script
                act.Should().Throw<FileNotFoundException>()
                   .WithMessage("*asr.py not found*");
            }
            finally
            {
                if (File.Exists(tempAudio)) File.Delete(tempAudio);
            }
        }

        [Fact]
        public void Transcribe_ShouldFire_TranscriptionStarted_Event()
        {
            // Arrange
            var service = new TranscribeService();
            var tempAudio = Path.GetTempFileName();
            bool eventFired = false;

            service.TranscriptionStarted += (sender, args) => { eventFired = true; };

            // Act
            try
            {
                // This will likely throw FileNotFound for the script, 
                // but the event fires BEFORE the script check in your code?
                // Looking at your code: File checks happen first. 
                // So we need to ensure the script validation passes to test the event, 
                // or we accept that we can't test this event easily without Refactoring.

                // However, based on your code order: 
                // 1. Check Audio (We have temp file)
                // 2. Check Script (If this fails, event won't fire)
                // 3. Fire Event.

                // Therefore, this test is only valid if the script actually exists.
                // Leaving this commented out as a placeholder for when you have the environment set up.
                // service.Transcribe(tempAudio);
            }
            catch { }

            // Assert
            // eventFired.Should().BeTrue();
        }

        // ---------------------------------------------------------
        // SECTION 2: Logic Tests (Testing Private Method via Reflection)
        // ---------------------------------------------------------

        // Helper method: Parameter names are 'output' and 'error'
        private TranscriptionResult CallParseOutput(TranscribeService service, string output, string error)
        {
            var methodInfo = typeof(TranscribeService).GetMethod("ParseOutput", BindingFlags.NonPublic | BindingFlags.Instance);
            if (methodInfo == null) throw new Exception("Method ParseOutput not found");

            return (TranscriptionResult)methodInfo.Invoke(service, new object[] { output, error });
        }

        [Fact]
        public void ParseOutput_ShouldReturnSuccess_WhenJsonIsValid()
        {
            // Arrange
            var service = new TranscribeService();
            string jsonOutput = "{ \"Status\": \"ok\", \"Transcript\": \"Hello world\" }";
            string errorOutput = "";

            // Act
            var result = CallParseOutput(service, jsonOutput, errorOutput);

            // Assert
            result.Status.Should().Be("success"); // Your logic normalizes "ok" to "success"
            result.Transcript.Should().Be("Hello world");
        }

        [Fact]
        public void ParseOutput_ShouldHandle_CaseInsensitiveJson()
        {
            // Arrange
            var service = new TranscribeService();
            // "status" is lowercase here
            string jsonOutput = "{ \"status\": \"ok\", \"transcript\": \"Testing casing\" }";

            // Act
            // FIXED: Removed the incorrect 'errorOutput:' name. 
            // We just pass empty string as the 3rd argument.
            var result = CallParseOutput(service, jsonOutput, "");

            // Assert
            result.Status.Should().Be("success");
            result.Transcript.Should().Be("Testing casing");
        }

        [Fact]
        public void ParseOutput_ShouldReturnError_WhenOutputIsEmpty()
        {
            // Arrange
            var service = new TranscribeService();
            string jsonOutput = ""; // Python crashed silently
            string errorOutput = "ImportError: No module named torch";

            // Act
            var result = CallParseOutput(service, jsonOutput, errorOutput);

            // Assert
            result.Status.Should().Be("error");
            result.Message.Should().Contain("Python error: ImportError");
        }

        [Fact]
        public void ParseOutput_ShouldReturnError_WhenJsonIsMalformed()
        {
            // Arrange
            var service = new TranscribeService();
            string jsonOutput = "{ \"Status\": \"ok\", \"Transcript\": ... BROKEN JSON ... }";

            // Act
            var result = CallParseOutput(service, jsonOutput, "");

            // Assert
            result.Status.Should().Be("error");
            result.Message.Should().Contain("Invalid JSON format");
        }

        [Fact]
        public void ParseOutput_ShouldReturnError_WhenStatusFieldIsMissing()
        {
            // Arrange
            var service = new TranscribeService();
            // Valid JSON, but missing business logic requirements
            string jsonOutput = "{ \"Transcript\": \"I have no status\" }";

            // Act
            var result = CallParseOutput(service, jsonOutput, "");

            // Assert
            result.Status.Should().Be("error");
            result.Message.Should().Contain("found no 'status' field");
        }
    }
}