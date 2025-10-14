using System;
using System.Collections.Generic;

namespace Parser
{
    public interface ITerminalParser
    {
        void Feed(ReadOnlySpan<byte> data);

        //event Action<IReadOnlyList<TerminalAction>> ActionsReady;
        event Action<byte[]> DcsResponse;

        ScreenBuffer Screenbuffer { get; }


    }

    // Placeholder – flyttas eller byggs ut senare
    public abstract record TerminalAction;
    public record PrintText(string Text) : TerminalAction;
    public record MoveCursor(int Row, int Col) : TerminalAction;
    public record ClearScreen() : TerminalAction;
    public record SetModeAction(string Mode) : TerminalAction;
    public record DcsAction(string Content) : TerminalAction;

}