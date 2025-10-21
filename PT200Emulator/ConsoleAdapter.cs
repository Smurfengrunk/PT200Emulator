using InputHandler;
using System.Text;
using Transport;
using Parser;

public class ConsoleAdapter
{
    private readonly IInputMapper _mapper;
    private readonly Action<byte[]> _send;
    private readonly EchoFilter echoFilter = new();

    public ConsoleAdapter(IInputMapper mapper, Action<byte[]> send)
    {
        _mapper = mapper;
        _send = send;
    }

#pragma warning disable CS8604
    public void Run()
    {
        while (true)
        {
          var keyInfo = Console.ReadKey(intercept: true);
            if ("åäöÅÄÖ".Contains(keyInfo.KeyChar))
            {
                var mapped = MapSwedishChar(keyInfo.KeyChar);
                _send(new byte[] { mapped });
                continue;
            }

            if (keyInfo.Key == ConsoleKey.Enter) _send(Encoding.ASCII.GetBytes("\r\n"));
            else if (keyInfo.Key == ConsoleKey.Backspace)
            {
                _send(new byte[] { 0x08 });
                Console.Write("\b \b"); // lokalt eko
                echoFilter.MarkBackspaceSent();
            }
            else if (SpecialKeyMap.TryGetValue(keyInfo.Key, out var scanCode))
            {
                var ev = new KeyEvent(scanCode, TranslateModifiers(keyInfo.Modifiers));
                var mapped = _mapper.MapKey(ev);
                if (mapped != null) _send(mapped);
            }
            else
            {
                _send(_mapper.MapText(keyInfo.KeyChar.ToString()));
            }
        }
    }
#pragma warning restore CS8604

    private KeyModifiers TranslateModifiers(ConsoleModifiers mods)
    {
        KeyModifiers km = KeyModifiers.None;
        if ((mods & ConsoleModifiers.Shift) != 0) km |= KeyModifiers.Shift;
        if ((mods & ConsoleModifiers.Control) != 0) km |= KeyModifiers.Ctrl;
        if ((mods & ConsoleModifiers.Alt) != 0) km |= KeyModifiers.Alt;
        return km;
    }

    private static readonly Dictionary<ConsoleKey, int> SpecialKeyMap = new()
    {
        // Funktionstangenter
        { ConsoleKey.F1,  0x3B },
        { ConsoleKey.F2,  0x3C },
        { ConsoleKey.F3,  0x3D },
        { ConsoleKey.F4,  0x3E },
        { ConsoleKey.F5,  0x3F },
        { ConsoleKey.F6,  0x40 },
        { ConsoleKey.F7,  0x41 },
        { ConsoleKey.F8,  0x42 },
        { ConsoleKey.F9,  0x43 },
        { ConsoleKey.F10, 0x44 },
        { ConsoleKey.F11, 0x57 },
        { ConsoleKey.F12, 0x58 },

        // Piltangenter
        { ConsoleKey.LeftArrow,  0xE04B },
        { ConsoleKey.RightArrow, 0xE04D },
        { ConsoleKey.UpArrow,    0xE048 },
        { ConsoleKey.DownArrow,  0xE050 },

        // Navigering
        { ConsoleKey.Home,       0xE047 },
        { ConsoleKey.End,        0xE04F },
        { ConsoleKey.Insert,     0xE052 },
        { ConsoleKey.Delete,     0xE053 },
        { ConsoleKey.PageUp,     0xE049 },
        { ConsoleKey.PageDown,   0xE051 },

        // Enter, Tab, Escape
        { ConsoleKey.Enter,      0x1C },
        { ConsoleKey.Tab,        0x0F },
        { ConsoleKey.Escape,     0x01 },
    };

    private static byte MapSwedishChar(char c)
    {
        return c switch
        {
            'Å' => (byte)'[',  // 0x5B
            'Ö' => (byte)'\\', // 0x5C
            'Ä' => (byte)']',  // 0x5D
            'å' => (byte)'{',  // 0x7B
            'ö' => (byte)'|',  // 0x7C
            'ä' => (byte)'}',  // 0x7D
            _ => (byte)c
        };
    }
}