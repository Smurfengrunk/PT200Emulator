using Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Util;
using static Parser.ScreenBuffer;

namespace Rendering
{
    public class ConsoleRenderer
    {
        private char[,] _lastFrame;
        private bool _initialized = false;

        public void Render(ScreenBuffer buffer)
        {
            if (buffer.clearScreen)
            {
                _lastFrame.Initialize();
                Console.Clear();
                buffer.ScreenCleared();
            }

            if (_lastFrame == null || _lastFrame.GetLength(0) != buffer.Rows || _lastFrame.GetLength(1) != buffer.Cols)
            {
                _lastFrame = new char[buffer.Rows, buffer.Cols];
                _initialized = false;
            }

            if (!_initialized)
            {
                // full draw once, then snapshot
                for (int row = 0; row < buffer.Rows; row++)
                {
                    Console.SetCursorPosition(0, row);
                    for (int col = 0; col < buffer.Cols; col++)
                    {
                        var ch = buffer.GetCell(row, col).Char;
                        var outCh = ch == '\0' ? ' ' : ch;
                        RenderCell(buffer.GetCell(row, col));
                        _lastFrame[row, col] = outCh;
                    }
                }
                _initialized = true;
                return;
            }

            // normal diff pass
            this.LogDebug("Normal diff pass");
            for (int row = 0; row < buffer.Rows; row++)
            {
                bool rowDirty = false;
                for (int col = 0; col < buffer.Cols; col++)
                {
                    var ch = buffer.GetCell(row, col).Char;
                    var outCh = ch == '\0' ? ' ' : ch;
                    if (_lastFrame[row, col] != outCh) { rowDirty = true; break; }
                }

                if (rowDirty)
                {
                    this.LogDebug("RowDirty diff pass");
                    Console.SetCursorPosition(0, row);
                    for (int col = 0; col < buffer.Cols; col++)
                    {
                        var ch = buffer.GetCell(row, col).Char;
                        var outCh = ch == '\0' ? ' ' : ch;
                        _lastFrame[row, col] = outCh;
                        RenderCell(buffer.GetCell(row, col));
                    }
                }
            }
        }

        private void RenderCell(ScreenCell cell)
        {
            var fg = cell.Style.Foreground;
            var bg = cell.Style.Background;
            this.LogTrace($"Reverse video for current cell {cell.Style.ReverseVideo}");

            if (cell.Style.ReverseVideo)
            {
                var tmp = fg;
                fg = bg;
                bg = tmp;
            }

            // ev. LowIntensity → mörkare färg
            if (cell.Style.LowIntensity)
            {
                fg = fg / 2;
            }

            //Console.ForegroundColor = MapBrushToConsoleColor(fg);
            //Console.BackgroundColor = MapBrushToConsoleColor(bg);

            Console.Write(cell.Char == '\0' ? ' ' : cell.Char);

            Console.ResetColor();
        }
    }
}
