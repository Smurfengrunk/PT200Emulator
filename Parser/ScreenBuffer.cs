using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Util;
using static System.Net.Mime.MediaTypeNames;

namespace Parser
{
    public class ScreenBuffer
    {
        private const int TabSize = 8;
        public event Action BufferUpdated;
        public event Action<int, int> CursorMoved;
        internal int _updateDepth;
        private bool _dirty;
        private bool _hasFlushed = false;
        private bool _inSystemLine = false;
        private CancellationTokenSource _idleCts;
        private readonly TimeSpan _idleDelay = TimeSpan.FromMilliseconds(8);
        private readonly CharTableManager charTableManager;
        //private FlushTrigger flushTrigger;
        private TerminalControl terminalControl;
        private CaretController _caretController;
        private char[,] _chars;
        public int Rows => _chars.GetLength(0);
        public int Cols => _chars.GetLength(1);
        private CancellationTokenSource _burstCts;
        private IDisposable _burstScope;
        public int _dirtyCount = 0;
        private int ScrollTop, ScrollBottom;
        public int CursorRow { get; private set; }
        public int CursorCol { get; private set; }
        public struct ScreenCell
        {
            public char Char;
            public Brush Foreground;
            public Brush Background;
            public StyleInfo Style;
        }

        private ScreenCell[,] _mainBuffer;
        public readonly ScreenCell[] _systemLineBuffer;
        public RowLockManager RowLocks { get; } = new();
        public StyleInfo CurrentStyle { get; set; } = new StyleInfo();
        public ScreenBuffer() { }
        public IDisposable BeginUpdate() => new UpdateScope(this);

        private void EnterUpdate() => Interlocked.Increment(ref _updateDepth);

        private readonly struct UpdateScope : IDisposable
        {
            private readonly ScreenBuffer _b;
            public UpdateScope(ScreenBuffer b) { _b = b; _b.EnterUpdate(); }
            public void Dispose() => _b.LeaveUpdate();
        }

        private void LeaveUpdate()
        {
            if (Interlocked.Decrement(ref _updateDepth) == 0 && _dirty)
            {
                // Schemalägg flush istället för att göra den direkt
                ScheduleIdleFlush();
            }
        }

        public void ForceEndUpdate()
        {
            while (_updateDepth > 0)
            {
                _updateDepth--;
            }
            try
            {
                _burstCts?.Cancel();
            }
            catch
            {
            }
            _burstCts = null;

            _burstScope?.Dispose();
            _burstScope = null;

            //_burstActive = false;

            _dirty = true;
        }

        private void ScheduleIdleFlush()
        {
            if (_dirtyCount == 0)
            {
                this.LogTrace("[BUFFER] Flush skipped – no mutations");
                return;
            }

            _idleCts?.Cancel();
            _idleCts = new CancellationTokenSource();
            var token = _idleCts.Token;

            var mutations = _dirtyCount;
            this.LogTrace($"[BUFFER] Flushing {mutations} mutations");

            // coalesce current batch
            _dirtyCount = 0;

            Task.Delay(_idleDelay, token).ContinueWith(_ =>
            {
                this.LogTrace($"[SCHEDULEDIDLEFLUSH] BufferUpdated invoked – handlers: {BufferUpdated?.GetInvocationList().Length ?? 0}, hash: {GetHashCode()}");
                if (!token.IsCancellationRequested && _dirty && _updateDepth == 0)
                {
                    this.LogTrace("[SCHEDULEIDLEFLUSH] Dags för en uppdatering");
                }
            }, TaskScheduler.Default);
        }
        public void Resize(int rows, int cols)
        {
            ForceEndUpdate();
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

                _dirty = true;
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

        public void WriteChar(char ch)
        {
            if (_caretController == null) _caretController = terminalControl.caretController;

            // new cycle starts when first mutation arrives
            if (_dirtyCount == 0) _hasFlushed = false;

            var glyph = charTableManager.Translate((byte)ch);

            var wroteRow = CursorRow;
            var wroteCol = CursorCol;

            //terminalControl.TerminalGrid.MarkCellDirty(wroteRow, wroteCol);

            _dirtyCount++;
            _dirty = true;
            if (_dirtyCount == 1) this.LogTrace("[BUFFER] First mutation since last flush");

            //flushTrigger.OnCharWritten(); // ensure this only schedules if dirty and not already scheduled
        }


        public void SetCursorPosition(int row, int col)
        {
            this.LogTrace($"[CURSOR] Pos=({CursorRow},{CursorCol})");
            CursorRow = Math.Clamp(row, 0, Rows - 1);
            CursorCol = Math.Clamp(col, 0, Cols - 1);
            CursorMoved?.Invoke(CursorRow, CursorCol);
            MarkDirty();
            if (!_hasFlushed && row == 22 && col == 0)
            {
                //flushTrigger.ForceFlush("Initsekvens avslutad");
                _hasFlushed = true;
            }
            //else flushTrigger.OnCursorMoved(CursorRow, CursorCol);
        }

        public void CarriageReturn()
        {
            if (_caretController == null) _caretController = terminalControl.caretController;
            CursorCol = 0;
            MarkDirty();
            _caretController.SetCaretPosition(CursorRow, CursorCol);
        }

        public void LineFeed()
        {
            if (_caretController == null) _caretController = terminalControl.caretController;
            CursorRow++;
            if (CursorRow >= Rows)
            {
                ScrollUp();
                CursorRow = Rows - 1;
            }
            MarkDirty();
            _caretController.MoveCaret(1, 0);
        }

        public void Backspace()
        {
            if (_caretController == null) _caretController = terminalControl.caretController;
            if (CursorCol > 0)
            {
                CursorCol--;
                MarkDirty();
            }
            _caretController.MoveCaret(0, -1);
        }

        public void Tab()
        {
            if (_caretController == null) _caretController = terminalControl.caretController;
            int nextStop = ((CursorCol / TabSize) + 1) * TabSize;
            CursorCol = Math.Min(nextStop, Cols - 1);
            MarkDirty();
            _caretController.SetCaretPosition(CursorRow, CursorCol);
        }

        public void ClearScreen()
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    _chars[r, c] = ' ';
                    _mainBuffer[r, c].Style = new StyleInfo();
                }

            CursorRow = 0;
            CursorCol = 0;
            MarkDirty();
            if (_caretController == null) return;
            _caretController.SetCaretPosition(CursorRow, CursorCol);
            _dirtyCount++;
            if (_dirtyCount == 1) this.LogTrace("[BUFFER] First mutation since last flush");
        }

        public void ClearLine(int row)
        {
            if ((uint)row >= (uint)Rows) return;
            for (int c = 0; c < Cols; c++)
            {
                _chars[row, c] = ' ';
                _mainBuffer[row, c].Style = new StyleInfo();
            }
            MarkDirty();
        }

        private void AdvanceCursor()
        {
            if (_caretController == null) _caretController = terminalControl.caretController;
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
            if (_caretController == null) _caretController = terminalControl.caretController;
            for (int r = 1; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    _chars[r - 1, c] = _chars[r, c];
                    _mainBuffer[r - 1, c].Style = _mainBuffer[r, c].Style;
                    //terminalControl.TerminalGrid.MarkCellDirty(CursorRow, CursorCol);
                }

            for (int c = 0; c < Cols; c++)
            {
                _chars[Rows - 1, c] = ' ';
                _mainBuffer[Rows - 1, c].Style = new StyleInfo();
            }
            MarkDirty();
            _caretController.SetCaretPosition(CursorRow, CursorCol);
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
    }

    public class StyleInfo
    {
        public int Foreground { get; set; } = Brushes.LimeGreen;
        public int Background { get; set; } = Brushes.Black;

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
            Foreground = Brushes.LimeGreen;
            Background = Brushes.Black;
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
            return (StyleInfo)MemberwiseClone();
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
