using System;
using System.Collections.Generic;
using System.Text;

namespace ExperimentASR.Services
{
    public static class WerCalculator
    {
        public static double CalculateWer(string reference, string hypothesis)
        {
            var refWords = UkrainianNormalizer.Normalize(reference).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var hypWords = UkrainianNormalizer.Normalize(hypothesis).Split(' ', StringSplitOptions.RemoveEmptyEntries);

            int substitutions = 0, deletions = 0, insertions = 0;
            LevenshteinDistance(refWords, hypWords, ref substitutions, ref deletions, ref insertions);

            int totalErrors = substitutions + deletions + insertions;
            return refWords.Length == 0 ? 0 : (double)totalErrors / refWords.Length * 100.0;
        }

        private static int LevenshteinDistance(string[] s, string[] t, ref int sCount, ref int dCount, ref int iCount)
        {
            // класичний алгоритм Вагнера-Фішера для масивів слів
            // (повний код нижче — 40 рядків)
            throw new NotImplementedException("LevenshteinDistance method is not implemented.");
        }
    }
}
