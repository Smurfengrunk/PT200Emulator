using System.Text;

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
        public bool inEmacs { get; private set; }

#pragma warning disable CS0219
        public EscapeSequenceHandler(CharTableManager charTables, ScreenBuffer buffer, TerminalControl terminal, TerminalState termstate)
        {
            this.charTableManager = charTables ?? throw new ArgumentNullException(nameof(charTables));
            _buffer = buffer;
            _terminal = terminal;
            _termState = termstate;
            _commandDecoder = new CompressedCommandDecoder(buffer);
        }
#pragma warning restore CS0219

        /// <summary>
        /// Tar emot en ESC-sekvens (utan själva ESC-tecknet) och utför rätt åtgärd.
        /// </summary>
        public void Handle(string sequence)
        {
            this.LogDebug($"[EscapeSequenceHandler] sequence = {sequence}");
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
                        case "F":
                            ManualInputEnabled = false;
                            break;
                        case "G":
                            ManualInputEnabled = true;
                            break;
                        case "O": // Save cursor and attributes
                            _buffer.setCA();
                            break;
                        case "Q": // restore cursor and attributes
                            _buffer.getCA();
                            break;
                        default:
                            this.LogWarning($"Okänd ESC $‑kod: {sequence:X2}");
                            break;
                    }
                    break;
                case "`": // ESC `
                    inEmacs = true;
                    ManualInputEnabled = false;
                    break;

                case "b": // ESC b
                    ManualInputEnabled = true;
                    break;
                case "0":
                    byte[] tmp = Encoding.ASCII.GetBytes(sequence);
                    _commandDecoder.HandleEscO(tmp[1], tmp[2]);
                    break;
                case "?":
                    _buffer.ClearScreen(); // Rensa hela skärmen
                    _buffer.CurrentStyle.Reset(); // Återställ stil
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

        public void HandleEscO(byte rowByte, byte colByte)
        {
            int row = rowByte - 0x20; // ger 1–48
            int col = colByte - 0x20; // ger 1–94

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

            // justera här om SetCursorPosition förväntar sig 0‑index
            _screen.SetCursorPosition(row, col);
        }
    }
}