using System.IO.Compression;
using System.Net;
using System.Text;
using FluentAssertions; // Optional, makes assertions easier to read
using Moq;
using Moq.Protected;
using SpeechMaster.Services;
using Xunit;

namespace SpeechMaster.Tests
{
	public class EngineSetupServiceTests : IDisposable
	{
		private readonly string _testDir;
		private readonly Mock<HttpMessageHandler> _httpHandlerMock;
		private readonly HttpClient _mockHttpClient;

		public EngineSetupServiceTests()
		{
			// 1. Create a unique temporary folder for each test
			_testDir = Path.Combine(Path.GetTempPath(), "SpeechMaster_Tests", Guid.NewGuid().ToString());
			Directory.CreateDirectory(_testDir);

			// 2. Setup Mock HTTP Handler
			_httpHandlerMock = new Mock<HttpMessageHandler>();
			_mockHttpClient = new HttpClient(_httpHandlerMock.Object);
		}

		public void Dispose()
		{
			// Cleanup temp folder after test finishes
			if (Directory.Exists(_testDir))
				Directory.Delete(_testDir, true);
			_mockHttpClient.Dispose();
		}

		[Fact]
		public void IsEngineInstalled_ShouldReturnFalse_WhenFilesMissing()
		{
			// Arrange
			var service = new EngineManager(_testDir, _mockHttpClient);

			// Act
			bool result = service.IsWhisperEngineInstalled();

			// Assert
			result.Should().BeFalse();
		}

		[Fact]
		public void IsEngineInstalled_ShouldReturnTrue_WhenFilesExist()
		{
			// Arrange
			var service = new EngineManager(_testDir, _mockHttpClient);

			// Simulate installed files
			string toolsDir = Path.Combine(_testDir, "Tools", "whisper");
			string modelsDir = Path.Combine(_testDir, "Models");
			Directory.CreateDirectory(toolsDir);
			Directory.CreateDirectory(modelsDir);

			File.WriteAllText(Path.Combine(toolsDir, "whisper-cli.exe"), "fake exe");
			File.WriteAllText(Path.Combine(modelsDir, "ggml-base.bin"), "fake model");

			// Act
			bool result = service.IsWhisperEngineInstalled();

			// Assert
			result.Should().BeTrue();
		}

		[Fact]
		public async Task EnsureEngineExistsAsync_ShouldDownloadAndExtract_WhenMissing()
		{
			// Arrange
			// 1. Mock GitHub API Response (JSON)
			string fakeDownloadUrl = "https://github.com/fake/download/whisper-bin-x64.zip";
			string githubJsonResponse = $@"{{
                ""assets"": [
                    {{ ""name"": ""whisper-bin-x64.zip"", ""browser_download_url"": ""{fakeDownloadUrl}"" }}
                ]
            }}";

			SetupMockResponse("https://api.github.com/repos/ggerganov/whisper.cpp/releases/latest", githubJsonResponse);

			// 2. Mock the ZIP file download
			// We create a real valid zip in memory containing our target file
			byte[] validZipBytes = CreateInMemoryZip(new Dictionary<string, string>
			{
				{ "whisper-bin-x64/whisper-cli.exe", "fake exe content" }, // Nested inside folder
                { "whisper-bin-x64/core.dll", "fake dll content" }         // Nested dll
            });

			SetupMockResponse(fakeDownloadUrl, validZipBytes);

			var service = new EngineManager(_testDir, _mockHttpClient);

			// Act
			await service.EnsureEngineExistsAsync();

			// Assert
			string expectedExePath = Path.Combine(_testDir, "Tools", "whisper", "whisper-cli.exe");
			string expectedDllPath = Path.Combine(_testDir, "Tools", "whisper", "core.dll");

			File.Exists(expectedExePath).Should().BeTrue("EXE should be extracted");
			File.Exists(expectedDllPath).Should().BeTrue("DLL should be extracted");

			// Check flattening logic: Ensure files are in root of Tools/whisper, not inside subfolder
			File.Exists(Path.Combine(_testDir, "Tools", "whisper", "whisper-bin-x64", "whisper-cli.exe"))
				.Should().BeFalse("Folder structure should be flattened");
		}

		[Fact]
		public async Task EnsureEngineExistsAsync_ShouldThrow_IfZipDoesNotContainExe()
		{
			// Arrange
			string fakeDownloadUrl = "https://github.com/fake/download/whisper-bin-x64.zip";

			SetupMockResponse("https://api.github.com/repos/ggerganov/whisper.cpp/releases/latest", $@"{{
                ""assets"": [ {{ ""name"": ""whisper-bin-x64.zip"", ""browser_download_url"": ""{fakeDownloadUrl}"" }} ]
            }}");

			// Create a zip WITHOUT whisper-cli.exe
			byte[] invalidZipBytes = CreateInMemoryZip(new Dictionary<string, string>
			{
				{ "readme.txt", "nothing here" }
			});

			SetupMockResponse(fakeDownloadUrl, invalidZipBytes);

			var service = new EngineManager(_testDir, _mockHttpClient);

			// Act & Assert
			await Assert.ThrowsAsync<FileNotFoundException>(() => service.EnsureEngineExistsAsync());
		}

		// --- Helpers ---

		private void SetupMockResponse(string url, string content)
		{
			SetupMockResponse(url, Encoding.UTF8.GetBytes(content));
		}

		private void SetupMockResponse(string url, byte[] content)
		{
			_httpHandlerMock
				.Protected()
				.Setup<Task<HttpResponseMessage>>(
					"SendAsync",
					ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == url),
					ItExpr.IsAny<CancellationToken>()
				)
				.ReturnsAsync(new HttpResponseMessage
				{
					StatusCode = HttpStatusCode.OK,
					Content = new ByteArrayContent(content)
				});
		}

		private byte[] CreateInMemoryZip(Dictionary<string, string> files)
		{
			using (var memoryStream = new MemoryStream())
			{
				using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
				{
					foreach (var file in files)
					{
						var entry = archive.CreateEntry(file.Key);
						using (var entryStream = entry.Open())
						using (var writer = new StreamWriter(entryStream))
						{
							writer.Write(file.Value);
						}
					}
				}
				return memoryStream.ToArray();
			}
		}
	}
}