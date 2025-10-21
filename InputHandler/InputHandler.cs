using System.Text.Json;
using System.Text.Json.Serialization;

namespace InputHandler
{
    public class InputHandler
    {
#pragma warning disable CS8604
        public InputMapper inputMapper { get; private set; }
        private string _basePath = new(AppDomain.CurrentDomain.BaseDirectory);

        public InputHandler()
        {
            var json = File.ReadAllText(Path.Combine(_basePath, "Data", "keymap.json"));
            var doc = JsonDocument.Parse(json);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var wrapper = JsonSerializer.Deserialize<KeyMap>(File.ReadAllText(Path.Combine(_basePath, "Data", "keymap.json")), options);
            inputMapper = new InputMapper(wrapper);

        }

        public class HexInt32Converter : JsonConverter<int>
        {
            public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString();
                    if (s != null && s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        return Convert.ToInt32(s, 16);
                    return int.Parse(s);
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    return reader.GetInt32();
                }
                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            {
                writer.WriteStringValue("0x" + value.ToString("X"));
            }
#pragma warning restore CS8604
        }
    }
}
