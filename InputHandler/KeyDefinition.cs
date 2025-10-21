using InputHandler;
using System.Text.Json.Serialization;

namespace InputHandler
{

#pragma warning disable CS8618
    public class KeyDefinition
    {
        [JsonConverter(typeof(InputHandler.HexInt32Converter))]
        public int ScanCode { get; set; }

        public string Key { get; set; }

        // Vanliga tecken
        public string Char { get; set; }
        public string ShiftChar { get; set; }
        public string AltChar { get; set; }
        public string AltGrChar { get; set; }

        // Escape-sekvenser för PT200
        public string Escape { get; set; }
        public string ShiftEscape { get; set; }
        public string CtrlEscape { get; set; }
        public string CtrlShiftEscape { get; set; }
    }
}

public class KeyMap
{
    public List<KeyDefinition> Keys { get; set; }
}
#pragma warning restore CS8618