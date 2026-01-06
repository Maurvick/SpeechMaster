using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ExperimentASR.Services
{
	// TODO: This is need to be improved further
    // based on specific normalization rules for Ukrainian text.
	public static class UkrainianNormalizer
    {
        private static readonly Regex Punctuation = new Regex(@"[‚„«»“”'""…]");
        private static readonly Regex MultipleSpaces = new Regex(@"\s+");

        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = text.ToLowerInvariant()
                       .Replace("’", "'")
                       .Replace("´", "'")
                       .Replace("`", "'")
                       .Replace("ґ", "г"); // або залишайте ґ — залежить від моделі

            text = Punctuation.Replace(text, "");
            text = MultipleSpaces.Replace(text, " ");
            return text.Trim();
        }
    }
}
