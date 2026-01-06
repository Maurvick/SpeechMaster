using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ExperimentASR.Services
{
	public class FFmpegLoader
	{
		public static void EnsureFfmpeg()
		{
			// 1. Check if the shell can find ffmpeg specifically
			if (IsFfmpegAvailableInShell())
			{
				Console.WriteLine("FFmpeg is ready to use.");
				return;
			}

			// 2. Install if missing
			Console.WriteLine("FFmpeg missing. Installing...");
			InstallFfmpegViaWinget();

			// 3. CRITICAL: Refresh PATH so we can use it immediately without restarting the app
			RefreshEnvironmentPath();

			// 4. Verify installation
			if (IsFfmpegAvailableInShell())
				Console.WriteLine("FFmpeg installed and verified successfully!");
			else
				Console.WriteLine("Installation finished, but FFmpeg is still not found. A restart may be required.");
		}

		// Uses the system "where" command to mimic exactly how Process.Start searches for files
		public static bool IsFfmpegAvailableInShell()
		{
			try
			{
				var processInfo = new ProcessStartInfo
				{
					FileName = "powershell",
					// This command:
					// 1. Rebuilds the PATH variable from the Registry (ignoring the C# app's old PATH)
					// 2. Checks if 'ffmpeg' exists using that fresh PATH
					// 3. Exits with 0 if found, 1 if missing
					Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" +
					"$env:Path = [System.Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path','User'); " +
					"if (Get-Command ffmpeg -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }\"",
					CreateNoWindow = true,
					UseShellExecute = false
				};

				using (var process = Process.Start(processInfo))
				{
					process.WaitForExit();
					return process.ExitCode == 0;
				}
			}
			catch
			{
				// If "where" command itself fails, fallback to assumption it's missing
				return false;
			}
		}

		public static void InstallFfmpegViaWinget()
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = "winget",
				// --silent disables most UI, --accept-* bypasses prompts
				Arguments = "install --id Gyan.FFmpeg -e --silent --accept-package-agreements --accept-source-agreements",
				UseShellExecute = false,
				CreateNoWindow = false // Let user see the installer progress or keep it hidden
			};

			var process = Process.Start(startInfo);
			process.WaitForExit();
		}

		// Updates the current process's PATH variable from the Machine/User registry
		public static void RefreshEnvironmentPath()
		{
			var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
			var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);

			// Combine them and update the current process environment
			var newPath = machinePath + ";" + userPath;
			Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Process);

			Console.WriteLine("Environment variables refreshed.");
		}
	}
}
