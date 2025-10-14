using Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
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
        public bool inEmacs { get; set; }
        private ScreenBuffer _screenBuffer;
        public void Render(ScreenBuffer buffer, bool inEmacs)
        {
            _screenBuffer = buffer;
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
                //this.LogDebug("First run - rendering whole screen");
                // full draw once, then snapshot
                for (int row = 0; row < buffer.Rows; row++)
                {
                    Console.SetCursorPosition(0, row);
                    for (int col = 0; col < buffer.Cols; col++)
                    {
                        var ch = buffer.GetCell(row, col).Char;
                        var outCh = ch == '\0' ? ' ' : ch;
                        RenderCell(buffer.GetCell(row, col), buffer.ZoneAttributes[row, col], inEmacs);
                        _lastFrame[row, col] = outCh;
                    }
                }
                _initialized = true;
                //this.LogDebug("Init render complete");
                return;
            }

            // normal diff pass
            //this.LogDebug("Diff pass");
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
                    //this.LogDebug("RowDirty pass");
                    Console.SetCursorPosition(0, row);
                    for (int col = 0; col < buffer.Cols; col++)
                    {
                        var ch = buffer.GetCell(row, col).Char;
                        var outCh = ch == '\0' ? ' ' : ch;
                        _lastFrame[row, col] = outCh;
                        //if (buffer.ZoneAttributes[row, col] != null) this.LogDebug($"[Render] buffer.ZoneAttributes[{row}, {col}].ReverseVideo = {buffer.ZoneAttributes[row, col].ReverseVideo}");
                        RenderCell(buffer.GetCell(row, col), buffer.ZoneAttributes[row, col], inEmacs);
                        //if (inEmacs) this.LogDebug($"[Render] Current cell at ({row}, {col})");
                    }
                    //this.LogDebug($"RowDirty pass completed for row {row}");
                }
            }
        }

        private void RenderCell(ScreenCell cell, StyleInfo zoneAttr, bool inEmacs)
        {
            var style = cell.Style.Clone();
            Color fg = (zoneAttr != null) ? zoneAttr.Foreground : cell.Style.Foreground;
            Color bg = (zoneAttr != null) ? zoneAttr.Background : cell.Style.Background;

            if (zoneAttr != null && zoneAttr.ReverseVideo)
            {
                var tmp = fg;
                fg = bg;
                bg = tmp;
                //this.LogTrace($"Reverse video for current cell {cell.Style.ReverseVideo}, Foreground = {fg.Name}, Background = {bg.Name}");
            }
            if (zoneAttr != null) this.LogDebug($"fg {zoneAttr.Foreground.Name}, bg {zoneAttr.Background.Name}");

            if (zoneAttr != null && zoneAttr.ReverseVideo) (style.Foreground, style.Background) = (style.Background, style.Foreground);

            // ev. LowIntensity → mörkare färg
            /*if (cell.Style.LowIntensity)
            {
                // Use Equals for struct comparison instead of switch (which requires constant values)
                if (fg.Equals(Brushes.Black)) fg = Brushes.Black_low;
                else if (fg.Equals(Brushes.White)) fg = Brushes.White_low;
                else if (fg.Equals(Brushes.Orange)) fg = Brushes.Orange_low;
                else if (fg.Equals(Brushes.LimeGreen)) fg = Brushes.LimeGreen_low;
                else if (fg.Equals(Brushes.Blue)) fg = Brushes.Blue_low;

                if (bg.Equals(Brushes.Black)) bg = Brushes.Black_low;
                else if (bg.Equals(Brushes.White)) bg = Brushes.White_low;
                else if (bg.Equals(Brushes.Orange)) bg = Brushes.Orange_low;
                else if (bg.Equals(Brushes.LimeGreen)) bg = Brushes.LimeGreen_low;
                else if (bg.Equals(Brushes.Blue)) bg = Brushes.Blue_low;

                this.LogTrace($"Low intensity video for current cell {cell.Style.LowIntensity}, Foreground = {fg.Name}, Background = {bg.Name}");
            }*/

            Console.ForegroundColor = MapToConsoleColor(fg);
            Console.BackgroundColor = MapToConsoleColor(bg);
            if (inEmacs) this.LogDebug($"[RenderCell] Position ({_screenBuffer.CursorRow}, {_screenBuffer.CursorCol}), Foreground {Console.ForegroundColor}, background {Console.BackgroundColor}, char {cell.Char}");
            Console.Write(cell.Char == '\0' ? ' ' : cell.Char);
            Console.ResetColor();
        }

        private static ConsoleColor MapToConsoleColor(Color color)
        {
            if (color.Equals(Brushes.Black)) return ConsoleColor.Black;
            else if (color.Equals(Brushes.White)) return ConsoleColor.White;
            else if (color.Equals(Brushes.Orange)) return ConsoleColor.DarkYellow;
            else if (color.Equals(Brushes.LimeGreen)) return ConsoleColor.Green;
            else if (color.Equals(Brushes.Blue)) return ConsoleColor.Blue;
            else return ConsoleColor.White;
        }
    }
}