using InputHandler;
using Parser;
using System;
using System.IO; // Viktigt för Path
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Transport;
using Util;

namespace Parser
{
    public class TerminalParser : ITerminalParser
    {
        private readonly CharTableManager charTables;
        private readonly EscapeSequenceHandler escHandler;
        private readonly PercentHandler percentHandler;
        private readonly OscHandler oscHandler;
        private readonly DollarCommandHandler dollarCommandHandler = new();
        public bool inEmacs => escHandler.inEmacs;
        public ScreenBuffer Screenbuffer { get; private set; }
        private List<byte> dcsBuffer = new();
        private readonly TerminalState termState;
        private List<byte> _csiBuffer = new();
        public DcsSequenceHandler _dcsHandler { get; private set; }
        public CsiSequenceHandler _csiHandler { get; private set; }
        public VisualAttributeManager visualAttributeManager { get; private set; }

        //public event Action<IReadOnlyList<TerminalAction>> ActionsReady;
        public event Action<byte[]> DcsResponse;
        public char Translate(byte code) => charTables.Translate(code);
        enum ParseState
        {
            Normal,
            Escape,
            CSI,
            DCS,
            OSC,
            Esc0,
            EscDollar,
            EscPercent,
            EscOther
        }
        private ParseState state = ParseState.Normal;
        private readonly StringBuilder seqBuffer = new();
        private readonly IInputHandler _controller;
        private readonly Dictionary<string, CsiCommandDefinition> _definitions;

        public TerminalParser(DataPathProvider paths, TerminalState state, IInputHandler controller, ModeManager modeManager, TerminalControl terminal)
        {
            var g0Path = Path.Combine(paths.CharTablesPath, "G0.json");
            var g1Path = Path.Combine(paths.CharTablesPath, "G1.json");
            Screenbuffer = new(state.Rows, state.Columns, AppDomain.CurrentDomain.BaseDirectory);

            var json = File.ReadAllText(Path.Combine(paths.BasePath, "data", "CsiCommands.json"));
            var root = JsonSerializer.Deserialize<CsiCommandRoot>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (root.CSI == null)
                throw new InvalidOperationException("Kunde inte läsa in CSI-kommandon från JSON.");

            var definitions = root.CSI;
            _definitions = definitions.ToDictionary(
                d => $"{d.Command}:{d.Params}",
                d => d,
                StringComparer.Ordinal
            );
            this.termState = state ?? throw new ArgumentNullException(nameof(state));
            _controller = controller;
            visualAttributeManager = new VisualAttributeManager();

            charTables = new CharTableManager(g0Path, g1Path);
            escHandler = new EscapeSequenceHandler(charTables, Screenbuffer, terminal, termState);

            _csiHandler = new CsiSequenceHandler(new CsiCommandTable(definitions, modeManager, visualAttributeManager, Screenbuffer, termState), modeManager);
            _dcsHandler = new DcsSequenceHandler(state, (Path.Combine(paths.BasePath, "Data", "DcsBitGroups.json")));
            percentHandler = new PercentHandler();
            oscHandler = new OscHandler();
            _dcsHandler.OnDcsResponse += bytes =>
            {
                this.LogTrace($"[PARSER] OnDcsResponse wired, handler hash={_dcsHandler.GetHashCode()}");
                DcsResponse?.Invoke(bytes);
            };

        }

        private void SetState(ParseState newState)
        {
            this.LogTrace($"[Parser] State → {newState}");
            state = newState;
        }

        private void ClearSeqBuffer()
        {
            this.LogDebug("[Parser] Sekvensbuffer rensad");
            seqBuffer.Clear();
        }

         /// <summary>
         /// Tar emot inkommande bytes och tolkar dem.
         /// </summary>
         /// 
        public void Feed(byte[] data) => Feed(data.AsSpan());
        public void Feed(ReadOnlySpan<byte> data)
        {
            if (Screenbuffer != null)
            {
                int previewLength = Math.Min(20, data.Length);
                string s_data = Encoding.UTF8.GetString(data);

                for (int i = 0; i < data.Length; i++)
                {
                    byte b = data[i];
                    switch (state)
                    {
                        case ParseState.Normal:
                            if (b == 0x1B) // ESC
                            {
                                state = ParseState.Escape;
                                seqBuffer.Clear();
                                continue;
                            }
                            switch (b)
                            {
                                case 0x08: Screenbuffer.Backspace(); break;
                                case 0x09: Screenbuffer.Tab(); break;
                                case 0x0A: Screenbuffer.LineFeed(); break;
                                case 0x0D: Screenbuffer.CarriageReturn(); break;
                                default:
                                    {
                                        seqBuffer.Append((char)b);
                                        Screenbuffer.WriteChar((char)b);
                                        if (escHandler.inEmacs) this.LogDebug($"{(char)b}");
                                        break;
                                    }
                            }
                            break;

                        case ParseState.Escape:
                            
                            if (b == '[')
                            {
                                state = ParseState.CSI;
                                seqBuffer.Clear();
                                //seqBuffer.Append((char)b);
                            }
                            else if (b == ']')
                            {
                                state = ParseState.OSC;
                                seqBuffer.Clear();
                                seqBuffer.Append((char)b);
                            }
                            else if (b == 'P')
                            {
                                state = ParseState.DCS;
                                seqBuffer.Clear();
                            }
                            else if (b == '$')
                            {
                                //this.LogTrace($"[Feed Esc] Escape {(char)b} detekterat");
                                state = ParseState.EscDollar;
                                //seqBuffer.Clear();
                                seqBuffer.Append((char)b);
                            }
                            else if (b == '0')
                            {
                                //this.LogTrace($"[Feed Esc] Escape {(char)b} detekterat");
                                state = ParseState.Esc0;
                                seqBuffer.Append((char)b);
                            }
                            else
                            {
                                seqBuffer.Append((char)b);
                                HandleEscOther(seqBuffer.ToString());
                                state = ParseState.Normal;
                            }
                            break;

                        case ParseState.CSI:
                            
                            seqBuffer.Append((char)b);
                            if (b >= 0x40 && b <= 0x7E)
                            {
                                HandleCsi((char)b, seqBuffer.ToString());
                                seqBuffer.Clear();
                                state = ParseState.Normal;
                            }
                            break;

                        case ParseState.OSC:
                            
                            seqBuffer.Append((char)b);
                            if (b == 0x07 || (seqBuffer.Length >= 2 && seqBuffer[^2] == 0x1B && seqBuffer[^1] == '\\'))
                            {
                                HandleOsc(seqBuffer.ToString());
                                seqBuffer.Clear();
                                state = ParseState.Normal;
                            }
                            break;

                        case ParseState.DCS:
                            
                            // Leta efter ESC \
                            if (b == 0x1B && i + 1 < data.Length && data[i + 1] == 0x5C)
                            {
                                this.LogTrace("[Parser Feed] Esc P Esc \\ - statusförfrågan");
                                HandleDcs(seqBuffer.ToString());
                                seqBuffer.Clear();
                                state = ParseState.Normal;
                                i++; // hoppa över '\'
                            }
                            else
                            {
                                seqBuffer.Append((char)b);
                            }
                            break;

                        case ParseState.EscDollar:
                            
                            seqBuffer.Append((char)b);
                            //this.LogTrace($"[Feed EscDollar] Escape {seqBuffer} detekterat");

                            if (seqBuffer.Length == 2)
                            {
                                HandleEscOther(seqBuffer.ToString());
                                seqBuffer.Clear();
                                state = ParseState.Normal;
                            }
                            break;

                        case ParseState.Esc0:
                            
                            seqBuffer.Append((char)b);
                            this.LogTrace($"[Feed Esc0] Escape {seqBuffer} detekterat");

                            if (seqBuffer.Length == 3)
                            {
                                HandleEscOther(seqBuffer.ToString());
                                seqBuffer.Clear();
                                state = ParseState.Normal;
                            }
                            break;
                    }
                }
            }
        }

        public void Parse(string sequence)
        {
            if (sequence.StartsWith("\x1B["))
                HandleCsi((char)sequence[^1], sequence);
            else if (sequence.StartsWith("\x1B]"))
                HandleOsc(sequence);
            else if (sequence.StartsWith("\x1B$"))
                HandleCharset(sequence);
            else if (sequence.StartsWith("\x1B%"))
                HandleEncoding(sequence);
            else if (sequence.StartsWith("\x1BP"))
                _dcsHandler.Handle(Encoding.ASCII.GetBytes(sequence));
            else
                HandleSingleEsc(sequence);
        }

        private void HandleSingleEsc(string sequence) => escHandler.Handle(sequence);
        private void HandleOsc(string sequence) => oscHandler.Handle(sequence);
        private void HandleEncoding(string sequence) => percentHandler.Handle(sequence);
        private void HandleCsi(char finalChar, string sequence) => _csiHandler.Handle(finalChar, sequence, termState, Screenbuffer, visualAttributeManager);
        private void HandleCharset(string sequence) => escHandler.Handle(sequence);
        private void HandleDcs(string sequence) => _dcsHandler.Handle(Encoding.ASCII.GetBytes(sequence));

        private void HandleEscPercent(char ch)
        {
            seqBuffer.Append(ch);
            if (ch >= '@' && ch <= 'G')
            {
                string percentSeq = seqBuffer.ToString();
                percentHandler.Handle(percentSeq);
                seqBuffer.Clear();
                state = ParseState.Normal;
            }
        }

        private void HandleEscOther(string escSeq)
        {
            escHandler.Handle(escSeq);
            seqBuffer.Clear();
            state = ParseState.Normal;
        }

        private void LogSequence(string label, string seq)
        {
            var hex = BitConverter.ToString(seq.Select(c => (byte)c).ToArray());
            this.LogDebug($"[{label}] \"{seq}\" | HEX: {hex}");
        }
    }

    public class OscHandler
    {
        public void Handle(string sequence)
        {
            this.LogDebug($"[OSC] {sequence}");
        }
    }

    public class DollarCommandHandler
    {
        public void Handle(string sequence)
        {
            this.LogDebug($"[ESC $] {sequence}");
        }
    }

    public class PercentHandler
    {
        public void Handle(string sequence)
        {
            this.LogDebug($"[ESC %] {sequence}");
        }
    }
}