using System;
using System.Collections.Generic;

namespace Parser
{
    public interface ITerminalParser
    {
        void Feed(ReadOnlySpan<byte> data);
        event Action<IReadOnlyList<TerminalAction>> ActionsReady;
        event Action<byte[]> OnDcsResponse;
        ScreenBuffer screenBuffer { get; }
        CsiSequenceHandler _csiHandler { get; }
        DcsSequenceHandler _dcsHandler { get; }
        EscapeSequenceHandler _escapeHandler { get; }
        TerminalParser terminalParser { get; }

    }

    // Placeholder – flyttas eller byggs ut senare
    public record TerminalAction(string Command, object Parameter = null);
}