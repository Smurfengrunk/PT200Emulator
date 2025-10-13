using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Parser;
using Util;

namespace Rendering
{
    public class ConsoleRenderer
    {
        public void Render(ScreenBuffer buffer)
        {
            if (buffer == null) return;

            if (buffer.clearScreen)
            {
                this.LogErr("Clear screen received");
                Console.Clear();
                buffer.ScreenCleared();
            }

            //Console.SetCursorPosition(0, 0);
            for (int row = 0; row < buffer.Rows; row++)
            {
                var line = new StringBuilder();
                for (int col = 0; col < buffer.Cols; col++)
                {
                    var cell = buffer.GetCell(row, col);
                    var ch = cell.Char == '\0' ? ' ' : cell.Char;
                    line.Append(ch);
                }
                var text = line.ToString().TrimEnd();
                Console.WriteLine(text);
            }
        }
    }
}
