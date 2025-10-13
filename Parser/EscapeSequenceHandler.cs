using System;
using System.Text;
using Util;

namespace Parser
{
    /// <summary>
    /// Tolkar ESC-sekvenser som påverkar teckentabellerna (G0/G1).
    /// </summary>
    public class EscapeSequenceHandler
    {
        private readonly CharTableManager charTableManager;
        private readonly ScreenBuffer _buffer;
        private readonly TerminalControl _terminal;
        private readonly TerminalState _termState;
        private CompressedCommandDecoder _commandDecoder;

        public bool ManualInputEnabled { get; private set; }
        public bool inEmacs {  get; private set; }

        public EscapeSequenceHandler(CharTableManager charTables, ScreenBuffer buffer, TerminalControl terminal, TerminalState termstate)
        {
            this.charTableManager = charTables ?? throw new ArgumentNullException(nameof(charTables));
            _buffer = buffer;
            _terminal = terminal;
            _termState = termstate;
            _commandDecoder = new CompressedCommandDecoder(buffer);
        }

        /// <summary>
        /// Tar emot en ESC-sekvens (utan själva ESC-tecknet) och utför rätt åtgärd.
        /// </summary>
        public void Handle(string sequence)
        {
            this.LogDebug($"[Handle] Escape sequence Esc {sequence}, HEX {BitConverter.ToString(sequence.Select(c => (byte)c).ToArray())}");
            switch (sequence.Substring(0, 1))
            {
                case "$":
                    switch (sequence.Substring(1, 1))
                    {
                        case "0": // ESC $ 0
                            charTableManager.LoadG0Ascii();
                            break;
                        case "1": // ESC $ 1
                            charTableManager.LoadG0Graphics();
                            break;
                        case "2": // ESC $ 2
                            charTableManager.LoadG1Ascii();
                            break;
                        case "3": // ESC $ 3
                            charTableManager.LoadG1Graphics();
                            break;
                        case "B":
                            _buffer.SetCursorPosition(0, 0);
                            break;
                        case "O": // Save cursor and attributes
                        case "Q": // restore cursor and attributes
                        case "G":
                            this.LogDebug($"[Handle] Esc {sequence} ignorerad");
                            break;
                        default:
                            this.LogWarning($"Okänd ESC $‑kod: {sequence:X2}");
                            break;
                    }
                    break;
                case "`": // ESC `
                    inEmacs = true;
                    this.LogDebug("[ESC] ESC ` – Disable Manual Input");
                    ManualInputEnabled = false;
                    break;

                case "b": // ESC b
                    this.LogDebug("[ESC] ESC b – Enable Manual Input");
                    ManualInputEnabled = true;
                    break;
                case "0":
                    this.LogDebug($"Escape sequence {sequence}");
                    byte[] tmp = Encoding.ASCII.GetBytes(sequence);
                    _commandDecoder.HandleEscO(tmp[0], tmp[1], tmp[2]);
                    break;
                case "?":
                    _buffer.ClearScreen(); // Rensa hela skärmen
                    _buffer.CurrentStyle.Reset(); // Återställ stil
                    _buffer.SetCursorPosition(0, 0); // Återställ cursor
                    this.LogDebug("[ESC] ESC ? → Clear screen + reset");
                    break;
            }
        }
    }

    public class CompressedCommandDecoder
    {
        private readonly ScreenBuffer _screen;

        public CompressedCommandDecoder(ScreenBuffer screen)
        {
            _screen = screen;
        }

        public void HandleEscO(byte rowByte, byte colByte, byte symbol)
        {
            int row = rowByte == 0 ? 1 : rowByte - 0x20;
            int col = colByte == 0 ? 1 : colByte - 0x20;

            if (row < 1 || row > 48 || col < 1 || col > 94)
            {
                this.LogWarning($"[ESCO] Ogiltig position: ({row},{col})");
                return;
            }

            if (_screen.InSystemLine())
            {
                this.LogWarning($"[ESCO] Cursor i systemlinje – position ({row},{col}) nekas");
                return;
            }

            if (_screen.RowLocks.IsLocked(row))
            {
                this.LogWarning($"[ESCO] Rad {row} är låst – cursorflytt nekas");
                return;
            }

            this.LogDebug($"[ESCO] Komprimerad cursorflytt till ({row},{col}, tecken att lägga in är {(char)symbol})");

            _screen.SetCursorPosition(row - 1, col - 1); // 0-indexerat internt
            _screen.WriteChar((char)symbol);
        }
    }
}