using Parser;
using Logging;

namespace Rendering
{
    public class ConsoleRenderer
    {
        private record struct RenderSnapshot
        {
            public char Char;
            public ConsoleColor Fg;
            public ConsoleColor Bg;
            public bool Reverse;
            public bool LowIntensity;

            public RenderSnapshot(char c, ConsoleColor fg, ConsoleColor bg, bool reverse, bool lowintensity)
            {
                Char = c;
                Fg = fg;
                Bg = bg;
                Reverse = reverse;
                LowIntensity = lowintensity;
            }
        }
        public bool Connected = true;
        private RenderSnapshot[,] _lastFrame;
        private bool _initialized = false;
        public bool inEmacs { get; set; }
        private ScreenBuffer _screenBuffer;
        private StyleInfo zoneAttrOld = new();
        private CaretController _caretController = new();

        public void ForceFullRender()
        {
            _initialized = false;
            _lastFrame = null;
        }
        public void Render(ScreenBuffer buffer, bool inEmacs)
        {
            _screenBuffer = buffer;
            _caretController.Hide();
            int runCharsCount = 0;

            if (buffer.clearScreen)
            {
                _initialized = false;
                Console.Clear();
                buffer.ScreenCleared();
            }
            if (buffer.forceRedraw) ForceFullRender();

            if (_lastFrame == null || _lastFrame.GetLength(0) != buffer.Rows || _lastFrame.GetLength(1) != buffer.Cols)
            {
                _lastFrame = new RenderSnapshot[buffer.Rows, buffer.Cols];
                _initialized = false;
            }

            // === INITIAL FULL DRAW ===
            if (!_initialized)
            {
                for (int row = 0; row < buffer.Rows; row++)
                {
                    int col = 0;
                    while (col < buffer.Cols)
                    {
                        var cell = buffer.GetCell(row, col);
                        var zone = buffer.ZoneAttributes[row, col];

                        StyleInfo.Color fg = zone?.Foreground ?? cell.Style.Foreground;
                        StyleInfo.Color bg = zone?.Background ?? cell.Style.Background;
                        bool reverse = zone?.ReverseVideo ?? cell.Style.ReverseVideo;
                        bool lowintensity = zone?.LowIntensity ?? cell.Style.LowIntensity;

                        if (reverse)
                        {
                            (fg, bg) = (bg, fg);
                        }
                        if (lowintensity)
                        {
                            if (reverse) bg = bg.MakeDim();
                            else fg = fg.MakeDim();
                        }

                        var outCh = cell.Char == '\0' ? ' ' : cell.Char;
                        var snap = new RenderSnapshot(outCh, MapToConsoleColor(fg), MapToConsoleColor(bg), reverse, lowintensity);

                        // starta run
                        int runStart = col;
                        var runFg = snap.Fg;
                        var runBg = snap.Bg;
                        var runChars = new List<char>();

                        while (col < buffer.Cols)
                        {
                            var c = buffer.GetCell(row, col);
                            var z = buffer.ZoneAttributes[row, col];
                            var f = z?.Foreground ?? c.Style.Foreground;
                            var b = z?.Background ?? c.Style.Background;
                            var r = z?.ReverseVideo ?? c.Style.ReverseVideo;
                            var l = z?.LowIntensity ?? c.Style.LowIntensity;

                            if (r) (f, b) = (b, f);
                            if (l)
                            {
                                if (r) b = b.MakeDim();
                                else f = f.MakeDim();
                            }

                            var ch = c.Char == '\0' ? ' ' : c.Char;
                            var s = new RenderSnapshot(ch, MapToConsoleColor(f), MapToConsoleColor(b), r, l);

                            if (!s.Equals(snap)) break;

                            runChars.Add(ch);
                            _lastFrame[row, col] = s;
                            col++;
                        }

                        Console.SetCursorPosition(runStart, row);
                        Console.ForegroundColor = runFg;
                        Console.BackgroundColor = runBg;
                        Console.Write(runChars.ToArray());
                    }
                }
                _initialized = true;
                buffer.forceRedraw = false;
                buffer.ClearDirty();
            }
            else
            {
                // === DIFF PASS ===
                for (int row = 0; row < buffer.Rows; row++)
                {
                    int col = 0;

                    while (col < buffer.Cols)
                    {
                        var cell = buffer.GetCell(row, col);
                        var zone = buffer.ZoneAttributes[row, col];

                        var fg = zone?.Foreground ?? cell.Style.Foreground;
                        var bg = zone?.Background ?? cell.Style.Background;
                        bool reverse = zone?.ReverseVideo ?? cell.Style.ReverseVideo;
                        bool lowintensity = zone?.LowIntensity ?? cell.Style.LowIntensity;

                        if (reverse) (fg, bg) = (bg, fg);
                        if (lowintensity)
                        {
                            if (reverse) bg = bg.MakeDim();
                            else fg = fg.MakeDim();
                        }

                        var outCh = cell.Char == '\0' ? ' ' : cell.Char;
                        var snap = new RenderSnapshot(outCh, MapToConsoleColor(fg), MapToConsoleColor(bg), reverse, lowintensity);

                        if (!_lastFrame[row, col].Equals(snap))
                        {
                            int runStart = col;
                            var runFg = snap.Fg;
                            var runBg = snap.Bg;
                            var runChars = new List<char>();

                            while (col < buffer.Cols)
                            {
                                var c = buffer.GetCell(row, col);
                                var z = buffer.ZoneAttributes[row, col];
                                var f = z?.Foreground ?? c.Style.Foreground;
                                var b = z?.Background ?? c.Style.Background;
                                var r = z?.ReverseVideo ?? c.Style.ReverseVideo;
                                var l = z?.LowIntensity ?? c.Style.LowIntensity;

                                if (r) (f, b) = (b, f);
                                if (l)
                                {
                                    if (r) b = b.MakeDim();
                                    else f = f.MakeDim();
                                }

                                var ch = c.Char == '\0' ? ' ' : c.Char;
                                var s = new RenderSnapshot(ch, MapToConsoleColor(f), MapToConsoleColor(b), r, l);

                                if (!s.Equals(snap)) break;

                                runChars.Add(ch);
                                _lastFrame[row, col] = s;
                                col++;
                            }

                            Console.SetCursorPosition(runStart, row);
                            Console.ForegroundColor = runFg;
                            Console.BackgroundColor = runBg;
                            Console.Write(runChars.ToArray());
                            runCharsCount++;
                        }
                        else
                        {
                            col++;
                        }
                    }
                }
            }
            runCharsCount = 0;
            _caretController.SetCaretPosition(buffer.CursorRow, buffer.CursorCol);
            _caretController.Show();
        }

        private static ConsoleColor MapToConsoleColor(StyleInfo.Color color)
        {
            if (color.Equals(StyleInfo.Color.Black) || color.Equals(StyleInfo.Color.Black_Low)) return ConsoleColor.Black;
            if (color.Equals(StyleInfo.Color.White)) return ConsoleColor.White;
            if (color.Equals(StyleInfo.Color.White_Low)) return ConsoleColor.Gray;
            if (color.Equals(StyleInfo.Color.Green)) return ConsoleColor.Green;
            if (color.Equals(StyleInfo.Color.Green_Low)) return ConsoleColor.DarkGreen;
            if (color.Equals(StyleInfo.Color.DarkYellow)) return ConsoleColor.DarkYellow;
            if (color.Equals(StyleInfo.Color.DarkYellow_Low)) return ConsoleColor.Yellow; // eller DarkYellow beroende på smak
            if (color.Equals(StyleInfo.Color.Blue)) return ConsoleColor.Blue;
            if (color.Equals(StyleInfo.Color.Blue_Low)) return ConsoleColor.DarkBlue;
            return ConsoleColor.White; // fallback
        }
    }

    public class ConsoleCaretController : ICaretController
    {
        public void SetCaretPosition(int row, int col)
            => Console.SetCursorPosition(col, row);

        public void MoveCaret(int dRow, int dCol)
            => Console.SetCursorPosition(Console.CursorLeft + dCol, Console.CursorTop + dRow);

        public void Show() => Console.CursorVisible = true;
        public void Hide() => Console.CursorVisible = false;
    }
}