namespace SpeechMaster.Services
{
	// FIXME: Unused class for benchmarking WER/CER calculations
	public static class WerCalculator
	{
		// Modified Levenshtein for WER calculation [Graves et al., 2006, adapted]
		private static int LevenshteinDistance(string[] refWords, string[] hypWords, out int substitutions, out int deletions, out int insertions)
		{
			int m = refWords.Length;
			int n = hypWords.Length;
			int[,] dp = new int[m + 1, n + 1];

			for (int i = 0; i <= m; i++) dp[i, 0] = i;
			for (int j = 0; j <= n; j++) dp[0, j] = j;

			for (int i = 1; i <= m; i++)
			{
				for (int j = 1; j <= n; j++)
				{
					int cost = refWords[i - 1] == hypWords[j - 1] ? 0 : 1;
					dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
				}
			}

			// Backtracing for S/D/I calculation
			substitutions = deletions = insertions = 0;
			int ii = m, jj = n;
			while (ii > 0 || jj > 0)
			{
				if (ii > 0 && jj > 0 && dp[ii, jj] == dp[ii - 1, jj - 1] + (refWords[ii - 1] == hypWords[jj - 1] ? 0 : 1))
				{
					if (refWords[ii - 1] != hypWords[jj - 1]) substitutions++;
					ii--; jj--;
				}
				else if (ii > 0 && dp[ii, jj] == dp[ii - 1, jj] + 1)
				{
					deletions++;
					ii--;
				}
				else
				{
					insertions++;
					jj--;
				}
			}

			return dp[m, n];
		}

		/// <summary>
		/// Calculates transcription WER, with Ukrainian text normalization
		/// </summary>
		/// <param name="reference"></param>
		/// <param name="hypothesis"></param>
		/// <returns></returns>
		public static double CalculateWer(string reference, string hypothesis)
		{
			string normRef = UkrainianNormalizer.Normalize(reference);
			string normHyp = UkrainianNormalizer.Normalize(hypothesis);

			string[] refWords = normRef.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			string[] hypWords = normHyp.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			int subs, dels, ins;
			int distance = LevenshteinDistance(refWords, hypWords, out subs, out dels, out ins);
			return refWords.Length == 0 ? 0 : (double)distance / refWords.Length * 100.0;
		}

		public static double CalculateCer(string reference, string hypothesis)
		{
			string normRef = UkrainianNormalizer.Normalize(reference);
			string normHyp = UkrainianNormalizer.Normalize(hypothesis);

			char[] refChars = normRef.ToCharArray();
			char[] hypChars = normHyp.ToCharArray();

			int subs, dels, ins;
			int distance = LevenshteinDistance(refChars.Select(c => c.ToString()).ToArray(), hypChars.Select(c => c.ToString()).ToArray(), out subs, out dels, out ins);
			return refChars.Length == 0 ? 0 : (double)distance / refChars.Length * 100.0;
		}
	}
}
