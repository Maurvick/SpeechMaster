using System.Text.RegularExpressions;

namespace SpeechMaster.Services
{
    public static class UkrainianNormalizer
    {
        private static readonly Regex Punctuation = new(@"[.,!?;:""„”«»—–-]", RegexOptions.Compiled);
        private static readonly Regex Apostrophe = new(@"['’]", RegexOptions.Compiled); // видаляємо апостроф як розділювач м'якості
        private static readonly Regex SoftSign = new(@"([дтзсцлншжч])ь", RegexOptions.Compiled | RegexOptions.IgnoreCase); // спрощуємо м'якість
        private static readonly Regex DoubleVowels = new(@"ії", RegexOptions.IgnoreCase); // 'ії' → 'ї' (типова помилка ASR)
        private static readonly Regex MultipleSpaces = new(@"\s+", RegexOptions.Compiled);

        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            text = text.ToLowerInvariant();

            // 1. Видаляємо апостроф (якщо не є частиною слова)
            text = Apostrophe.Replace(text, "");

            // 2. Спрощуємо м'якість (палаталізація): 'дь' → 'д', 'ть' → 'т' тощо
            text = SoftSign.Replace(text, "$1");

            // 3. Нормалізація 'ї/і' (подвійні голосні → одна)
            text = DoubleVowels.Replace(text, "ї");

            // 4. Видаляємо пунктуацію
            text = Punctuation.Replace(text, "");

            // 5. Нормалізуємо пробіли
            text = MultipleSpaces.Replace(text, " ").Trim();

            return text;
        }
    }
}
