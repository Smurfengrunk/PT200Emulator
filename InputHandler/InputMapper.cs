using System.Text;

namespace InputHandler
{
    public class InputMapper : IInputMapper
    {
        private readonly Dictionary<(int ScanCode, KeyModifiers Mods), byte[]> _lookup;

        public InputMapper(KeyMap keyMap)
        {
            _lookup = new();

            foreach (var def in keyMap.Keys)
            {
                int sc = def.ScanCode; // redan int i KeyDefinitions

                if (!string.IsNullOrEmpty(def.Escape)) _lookup[(sc, KeyModifiers.None)] = Encoding.ASCII.GetBytes(def.Escape);
                if (!string.IsNullOrEmpty(def.ShiftEscape)) _lookup[(sc, KeyModifiers.Shift)] = Encoding.ASCII.GetBytes(def.ShiftEscape);
                if (!string.IsNullOrEmpty(def.CtrlEscape)) _lookup[(sc, KeyModifiers.Ctrl)] = Encoding.ASCII.GetBytes(def.CtrlEscape);
                if (!string.IsNullOrEmpty(def.CtrlShiftEscape)) _lookup[(sc, KeyModifiers.Ctrl | KeyModifiers.Shift)] = Encoding.ASCII.GetBytes(def.CtrlShiftEscape);
                if (!string.IsNullOrEmpty(def.AltChar)) _lookup[(sc, KeyModifiers.Alt)] = Encoding.ASCII.GetBytes(def.AltChar);
                if (!string.IsNullOrEmpty(def.AltGrChar)) _lookup[(sc, KeyModifiers.AltGr)] = Encoding.ASCII.GetBytes(def.AltGrChar);
            }
        }

        public byte[]? MapKey(KeyEvent e)
        {
            return _lookup.TryGetValue((e.ScanCode, e.Modifiers), out var seq)
                ? seq
                : null;
        }

        public byte[]? MapText(string text)
        {
            return !string.IsNullOrEmpty(text)
                ? Encoding.ASCII.GetBytes(text)
                : null;
        }
    }
}