using System.Text.Json;

namespace Parser
{
    public class LocalizationProvider
    {
        private readonly Dictionary<string, string> strings;
        public string Language { get; }

        public LocalizationProvider(string languageCode = "sv")
        {
            Language = languageCode;
            var path = Path.Combine("Resources", $"Strings.{languageCode}.json");
            var json = File.ReadAllText(path);
            strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }

        public string Get(string key, params object[] args)
        {
            if (strings.TryGetValue(key, out var template))
                return string.Format(template, args);
            return $"{key}";
        }
    }
}
