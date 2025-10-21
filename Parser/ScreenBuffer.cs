
using Parser;

namespace Parser
{
#pragma warning disable CS8632
    public class ScreenBuffer
    {
        private const int TabSize = 8;
        public event Action BufferUpdated;
        public event Action Scrolled;
        private bool _inSystemLine = false;
        private readonly TimeSpan _idleDelay = TimeSpan.FromMilliseconds(8);
        private ICaretController? _caretController;
        private CharTableManager charTableManager;
        private char[,] _chars;
        public int Rows => _chars.GetLength(0);
        public int Cols => _chars.GetLength(1);
        private int ScrollTop, ScrollBottom;
        public int CursorRow { get; private set; }
        public int CursorCol { get; private set; }
        public bool clearScreen { get; private set; } = false;
        public bool forceRedraw { get; set; } = false;
        public void ScreenCleared() => clearScreen = false;
        private bool _updating;
        private bool _dirty;

        public struct ScreenCell
        {
            public char Char;
            public StyleInfo.Color Foreground;
            public StyleInfo.Color Background;
            public StyleInfo Style;
        }

        public struct SCA // Save Cursor and Attributes
        {
            public int row, col;
            public ScreenCell[,] cell;
            //relative screen position?
            //value of SGR command?
        }
        SCA sca;


        private ScreenCell[,] _mainBuffer;
        public readonly ScreenCell[] _systemLineBuffer;
        public StyleInfo[,] ZoneAttributes { get; private set; }
        public RowLockManager RowLocks { get; } = new();
        public StyleInfo CurrentStyle { get; set; } = new StyleInfo();
        public ScreenBuffer(int rows, int cols, string basePath)
        {
            _mainBuffer = new ScreenCell[rows, cols];
            _chars = new char[rows, cols];
            _systemLineBuffer = new ScreenCell[cols];
            ZoneAttributes = new StyleInfo[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    _mainBuffer[r, c] = new ScreenCell();
                    _mainBuffer[r, c].Style = new StyleInfo();
                    _chars[r, c] = ' ';
                }
            for (int c = 0; c < cols; c++) _systemLineBuffer[c] = new ScreenCell();

            if (rows <= 0 || cols <= 0) throw new ArgumentOutOfRangeException();
            var g0path = Path.Combine(basePath, "data", "chartables", "g0.json");
            var g1path = Path.Combine(basePath, "data", "chartables", "g1.json");
            charTableManager = new CharTableManager(g0path, g1path);
        }
        public void Resize(int rows, int cols)
        {
            _mainBuffer = new ScreenCell[rows, cols];

            if (rows <= 0 || cols <= 0) throw new ArgumentOutOfRangeException();

            var newChars = new char[rows, cols];
            var newStyles = new StyleInfo[rows, cols];

            // Kopiera så mycket som får plats från gamla bufferten
            for (int r = 0; r < Math.Min(Rows, rows); r++)
                for (int c = 0; c < Math.Min(Cols, cols); c++)
                {
                    newChars[r, c] = _chars[r, c];
                    newStyles[r, c] = _mainBuffer[r, c].Style;
                }
            // Kopiera så mycket som får plats från gamla bufferten
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    if (newStyles[r, c] == null)
                        _mainBuffer[r, c].Style = new StyleInfo();
                }

            _chars = newChars;

            CursorRow = Math.Min(CursorRow, rows - 1);
            CursorCol = Math.Min(CursorCol, cols - 1);

            MarkDirty();
        }

        public void AttachCaretController(ICaretController controller)
        {
            _caretController = controller;
        }

        public char GetChar(int row, int col)
        {
            var ch = _chars[row, col];

            return ((uint)row >= (uint)Rows || (uint)col >= (uint)Cols || ch == '\0') ? ' ' : _chars[row, col];
        }

        public char GetSystemLineChar(int col)
        {
            return ((uint)col >= (uint)Cols) ? ' ' : _systemLineBuffer[col].Char;
        }

        public StyleInfo GetStyle(int row, int col)
        {
            return ((uint)row >= (uint)Rows || (uint)col >= (uint)Cols) ? new StyleInfo() : _mainBuffer[row, col].Style;
        }

        public void BeginUpdate() => _updating = true;

        public void EndUpdate()
        {
            _updating = false;
            if (_dirty)
            {
                BufferUpdated?.Invoke();
                ClearDirty();
            }
        }


        public void WriteChar(char ch)
        {

            var wroteRow = CursorRow;
            var wroteCol = CursorCol;
            var cell = _mainBuffer[wroteRow, wroteCol];
            var style = CurrentStyle.Clone();

            if (ch == '\x1B') return;

            if (_inSystemLine)
            {
                if ((uint)wroteCol >= (uint)Cols) return;
                _systemLineBuffer[wroteCol] = new ScreenCell
                {
                    Char = ch,
                    //Foreground = CurrentStyle.Background,
                    //Background = CurrentStyle.Foreground,
                    Style = CurrentStyle.Clone()
                };
                CursorCol = Math.Min(wroteCol + 1, Cols - 1);
                _caretController.MoveCaret(0, 1);
            }
            else
            {
                if ((uint)wroteRow >= (uint)Rows || (uint)wroteCol >= (uint)Cols) return;

                // om cellen redan har ReverseVideo från ett område, bevara det
                if (cell.Style != null && cell.Style.ReverseVideo)
                    style.ReverseVideo = true;

                cell.Char = ch;
                cell.Style = style;
                cell.Foreground = style.ReverseVideo ? style.Background : style.Foreground;
                cell.Background = style.ReverseVideo ? style.Foreground : style.Background;

                _mainBuffer[wroteRow, wroteCol] = cell;
                _chars[wroteRow, wroteCol] = ch;

                AdvanceCursor();
            }
            MarkDirty();
            if (!_updating) BufferUpdated.Invoke();
        }


        public void SetCursorPosition(int row, int col)
        {
            CursorRow = Math.Clamp(row - 1, 0, Rows - 1);
            CursorCol = Math.Clamp(col - 1, 0, Cols - 1);
        }

        public void CarriageReturn()
        {
            CursorCol = 0;
            _caretController.SetCaretPosition(CursorRow, CursorCol);
        }

        public void LineFeed()
        {
            CursorRow++;
            if (CursorRow >= Rows)
            {
                ScrollUp();
                CursorRow = Rows - 1;
            }
            _caretController.MoveCaret(1, 0);
        }

        public void Backspace()
        {

            if (CursorCol > 0)
            {
                CursorCol--;
                // Radera tecknet i bufferten
                _chars[CursorRow, CursorCol] = '\0';
                _mainBuffer[CursorRow, CursorCol].Char = _chars[CursorRow, CursorCol];
                // Markera cellen som dirty
                MarkDirty();
            }

            _caretController.MoveCaret(0, -1);
        }

        public void Tab()
        {
            int nextStop = ((CursorCol / TabSize) + 1) * TabSize;
            CursorCol = Math.Min(nextStop, Cols - 1);
            _caretController.SetCaretPosition(CursorRow, CursorCol);
        }

        public void ClearScreen()
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    _chars[r, c] = ' ';
                    _mainBuffer[r, c].Char = _chars[r, c];
                    _mainBuffer[r, c].Style = new StyleInfo();
                }

            CursorRow = 0;
            CursorCol = 0;
            if (_caretController == null) return;
            _caretController.SetCaretPosition(CursorRow, CursorCol);
            clearScreen = true;
        }

        public void ClearLine(int scope)
        {
            if ((uint)CursorRow >= (uint)Rows || (uint)CursorCol >= (uint)Cols) return;

            switch (scope)
            {
                case 0: // cursor → EOL
                    for (int col = CursorCol; col < Cols; col++) ClearCell(CursorRow, col);
                    break;

                case 1: // BOL → cursor
                    for (int col = 0; col <= CursorCol; col++) ClearCell(CursorRow, col);
                    break;

                case 2: // hela raden
                    for (int col = 0; col < Cols; col++) ClearCell(CursorRow, col);
                    break;
            }
        }

        private void ClearCell(int row, int col)
        {
            _chars[row, col] = ' ';
            _mainBuffer[row, col].Char = _chars[row, col];
            _mainBuffer[row, col].Style = new StyleInfo();
            MarkDirty();
        }

        private void AdvanceCursor()
        {
            CursorCol++;
            if (CursorCol >= Cols)
            {
                CursorCol = 0;
                CursorRow++;
                if (CursorRow >= Rows)
                {
                    ScrollUp();
                    CursorRow = Rows - 1;
                }
            }
            _caretController.SetCaretPosition(CursorRow, CursorCol);
        }

        private void ScrollUp()
        {

            // Flytta upp alla rader
            for (int r = 1; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    _mainBuffer[r - 1, c] = _mainBuffer[r, c];   // kopiera hela cellen
                }
            }

            // Töm sista raden
            for (int c = 0; c < Cols; c++)
            {
                _mainBuffer[Rows - 1, c] = new ScreenCell
                {
                    Char = ' ',
                    Foreground = CurrentStyle.Foreground,
                    Background = CurrentStyle.Background,
                    Style = ZoneAttributes[Rows - 1, c] ?? new StyleInfo()
                };
            }

            // Markera hela bufferten som ändrad
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    MarkDirty();

            CursorRow = Rows - 1;
            _caretController.SetCaretPosition(CursorRow, CursorCol);
            Scrolled?.Invoke();
        }

        public void SetScrollRegion(int top, int bottom)
        {
            // Sätt övre och nedre gräns för scrollområdet
            ScrollTop = top;
            ScrollBottom = bottom;
            // Validera att top < bottom och inom skärmens höjd
        }

        public void ResetScrollRegion()
        {
            ScrollTop = 0;
            ScrollBottom = Rows - 1;
        }

        public ScreenCell GetCell(int row, int col)
        {
            return _mainBuffer[row, col];
        }

        public void SetCell(int row, int col, ScreenCell cell)
        {
            _mainBuffer[row, col] = cell;
        }

        public void SetStyle(int row, int col, StyleInfo style)
        {
            _mainBuffer[row, col].Style = style;
        }

        public int GetCursorPosition() { return 0; }
        public bool InSystemLine() { return false; }
        public void MarkDirty()
        {
            _dirty = true;
        }

        public void ClearDirty()
        {
            _dirty = false;
        }

        public bool GetDirty()
        {
            return _dirty;
        }

        public void setCA()
        {
            this.LogDebug($"setCA(), Row = {CursorRow}, Col = {CursorCol}, Backround for cell @(23,0) = {_mainBuffer[23,0].Background}");
            sca.row = CursorRow;
            sca.col = CursorCol;
            sca.cell = _mainBuffer;
        }

        public void getCA()
        {
            this.LogDebug($"getCA(), Row = {sca.row}, Col = {sca.col}, Backround for cell @(23,0) = {sca.cell[23, 0].Background}");
            CursorRow = sca.row;
            CursorCol = sca.col;
            _mainBuffer = sca.cell;
            forceRedraw = true;
            BufferUpdated.Invoke();
        }
    }
#pragma warning restore CS8632

    public class StyleInfo
    {
        public enum Color
        {
            Default,
            Black,
            DarkRed,
            DarkGreen,
            DarkYellow,
            DarkBlue,
            DarkMagenta,
            DarkCyan,
            Gray,
            Red,
            Green,
            Yellow,
            Blue,
            Magenta,
            Cyan,
            White,
            Default_Low,
            Black_Low,
            DarkRed_Low,
            DarkGreen_Low,
            DarkYellow_Low,
            DarkBlue_Low,
            DarkMagenta_Low,
            DarkCyan_Low,
            Gray_Low,
            Red_Low,
            Green_Low,
            Yellow_Low,
            Blue_Low,
            Magenta_Low,
            Cyan_Low,
            White_Low
        }


        public Color Foreground { get; set; } = StyleInfo.Color.Green;
        public Color Background { get; set; } = StyleInfo.Color.Black;

        public bool Blink { get; set; } = false;
        public bool Bold { get; set; } = false;
        public bool Underline { get; set; } = false;
        public bool ReverseVideo { get; set; } = false;
        public bool LowIntensity { get; set; } = false;
        public bool StrikeThrough { get; set; } = false;

        // PT200-specifika
        public bool Transparent { get; set; } = false;
        public bool VisualAttributeLock { get; set; } = false;
        //public FontFamily FontFamily { get; set; } = new FontFamily("Consolas");
        //public double FontSize { get; set; } = 14;

        public double ColumnWidth { get; private set; } = 8;
        public double RowHeight { get; private set; } = 17;


        public void Reset()
        {
            Foreground = StyleInfo.Color.Green;
            Background = StyleInfo.Color.Black;
            Blink = false;
            Bold = false;
            Underline = false;
            ReverseVideo = false;
            LowIntensity = false;
            Transparent = false;
            VisualAttributeLock = false;
            ColumnWidth = 8;
            RowHeight = 17;
        }

        public StyleInfo Clone()
        {
            return new StyleInfo
            {
                Foreground = this.Foreground,
                Background = this.Background,
                Blink = this.Blink,
                Bold = this.Bold,
                Underline = this.Underline,
                ReverseVideo = this.ReverseVideo,
                LowIntensity = this.LowIntensity,
                StrikeThrough = this.StrikeThrough,
                Transparent = this.Transparent,
                VisualAttributeLock = this.VisualAttributeLock,
                ColumnWidth = this.ColumnWidth,
                RowHeight = this.RowHeight
            };
        }
    }

    public static class StyleInfoExtensions
    {
        public static StyleInfo.Color MakeDim(this StyleInfo.Color color)
        {
            if (color.Equals(StyleInfo.Color.Black)) return StyleInfo.Color.Black_Low;
            if (color.Equals(StyleInfo.Color.White)) return StyleInfo.Color.White_Low;
            if (color.Equals(StyleInfo.Color.Green)) return StyleInfo.Color.Green_Low;
            if (color.Equals(StyleInfo.Color.DarkYellow)) return StyleInfo.Color.DarkYellow_Low;
            if (color.Equals(StyleInfo.Color.Blue)) return StyleInfo.Color.Blue_Low;
            return color;
        }
    }


    public class RowLockManager
    {
        private readonly HashSet<int> _lockedRows = new();
        private bool _ignoreLocksTemporarily = false;
        public IEnumerable<int> GetLockedRows() => _lockedRows.OrderBy(r => r);

        public void Lock(int row) => _lockedRows.Add(row);
        public void Unlock(int row) => _lockedRows.Remove(row);

        public void LockSystemLines(int top, int bottom)
        {
            for (int i = top; i <= bottom; i++)
                Lock(i);
        }

        public bool IsLocked(int row)
        {
            if (_ignoreLocksTemporarily) return false;
            return _lockedRows.Contains(row);
        }

        public void IgnoreLocksTemporarily()
        {
            _ignoreLocksTemporarily = true;
        }

        public void RestoreLockEnforcement()
        {
            _ignoreLocksTemporarily = false;
        }

        public void LogLockedRows()
        {
            var locked = GetLockedRows().ToList();
            if (locked.Count == 0)
                this.LogDebug("[RowLockManager] Inga låsta rader");
            else
                this.LogDebug($"[RowLockManager] Låsta rader: {string.Join(", ", locked)}");
        }
    }
}
