using Logging;

namespace Parser
{
    public class VisualAttributeManager
    {

        public void ChangeDisplayAttributes(int scope, string[] parameters, ScreenBuffer buffer, TerminalState terminal)
        {
            // Uppdatera aktuell stil
            if (parameters.Length > 0) HandleSGR(parameters, buffer, terminal);

            // Måla om området med den nya stilen
            ApplyStyleToScope(scope, buffer, buffer.CurrentStyle.Clone());
        }

        public void ApplyStyleToScope(int scope, ScreenBuffer buffer, StyleInfo style)
        {
            (int startRow, int startCol, int endRow, int endCol) = CalculateScope(scope, buffer);

            for (int row = startRow; row <= endRow; row++)
            {
                for (int col = (row == startRow ? startCol : 0);
                         col <= (row == endRow ? endCol : buffer.Cols - 1);
                         col++)
                {
                    buffer.ZoneAttributes[row, col] = style;
                }
            }
        }

        private (int, int, int, int) CalculateScope(int scope, ScreenBuffer buffer)
        {
            int startRow, startCol, endRow, endCol;

            switch (scope)
            {
                case 0: // från cursor till slutet
                    startRow = buffer.CursorRow;
                    startCol = buffer.CursorCol;
                    endRow = buffer.Rows - 1;
                    endCol = buffer.Cols - 1;
                    break;
                case 1: // från början till cursor
                    startRow = 0;
                    startCol = 0;
                    endRow = buffer.CursorRow;
                    endCol = buffer.CursorCol;
                    break;
                case 2: // hela skärmen
                default:
                    startRow = 0;
                    startCol = 0;
                    endRow = buffer.Rows - 1;
                    endCol = buffer.Cols - 1;
                    break;
            }
            return (startRow, startCol, endRow, endCol);
        }

        public void HandleSGR(string[] parameters, ScreenBuffer buffer, TerminalState terminal)
        {
            if (parameters.Length == 0)
            {
                buffer.CurrentStyle.Reset();
                buffer.forceRedraw = true;
                return;
            }

            foreach (var p in parameters)
            {
                switch (p)
                {
                    case "0":
                        buffer.CurrentStyle.Reset();
                        buffer.forceRedraw = true;
                        break;
                    case "2":
                        buffer.CurrentStyle.LowIntensity = true; // Lägg till flagga i CurrentStyle
                        break;
                    case "4":
                        buffer.CurrentStyle.Underline = true;
                        break;
                    case "5":
                        buffer.CurrentStyle.Blink = true;
                        break;
                    case "7":
                        buffer.CurrentStyle.ReverseVideo = true;
                        break;
                    case ">1":
                        buffer.CurrentStyle.StrikeThrough = true;
                        break;
                    case ">2":
                        buffer.CurrentStyle.Foreground = buffer.CurrentStyle.Background;
                        break;
                    case ">3":
                        this.LogDebug($"Line Drawing Graphics, ESC [{p}m");
                        break;
                    case ">4":
                        this.LogDebug($"Block Drawing Graphics, ESC [{p}m");
                        break;
                }
            }
        }

        public enum GlyphMode
        {
            Normal,
            LineDrawing,
            BlockDrawing
        }

        public class GlyphModeInterpreter
        {
            public void Apply(string[] parameters, TerminalState terminal)
            {
                foreach (var p in parameters)
                {
                    switch (p)
                    {
                        case ">3":
                            this.LogDebug($"Glyphmode {p} => Line Drawing");
                            break;
                        case ">4":
                            this.LogDebug($"Glyphmode {p} => Block Drawing");
                            break;
                        case "0":
                            this.LogDebug($"Glyphmode {p} => Normal");
                            break;
                            // Lägg till fler om PT200 har fler glyphlägen
                    }
                }
            }
        }
    }

    public class VisualAttributes
    {
        public bool Bold { get; set; }
        public bool Underline { get; set; }
        public bool ReverseVideo { get; set; }
        public ConsoleColor Foreground { get; set; }
        public ConsoleColor Background { get; set; }

        public void Reset()
        {
            Bold = false;
            Underline = false;
            ReverseVideo = false;
            Foreground = ConsoleColor.Gray;
            Background = ConsoleColor.Black;
        }
    }
}
