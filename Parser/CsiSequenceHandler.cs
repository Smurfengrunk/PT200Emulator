using Serilog;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Parser
{
    public class CsiSequenceHandler
    {
        private readonly CsiCommandTable table;
        private string paramStr, command;
        private string[] parameters;
        bool isPrivate;
        private ModeManager _modeManager;

        public CsiSequenceHandler(CsiCommandTable table, ModeManager modeManager)
        {
            this.table = table;
            _modeManager = modeManager;
        }

        public void Handle(char finalChar, string sequence, TerminalState terminal, ScreenBuffer buffer, VisualAttributeManager visualAttributeManager)
        {
            //this.LogDebug($"[CSI] Sekvens mottagen: ESC[{sequence}");
            paramStr = string.Empty;
            parameters = new string[0];
            // Exempel: "12;24H"
            var match = Regex.Match(sequence, @"^([><\?\d;]*)([A-Za-z])$");
            if (!match.Success || match.Groups.Count < 2) return;

            paramStr = match.Groups[1].Value;
            command = match.Groups[2].Value;
            isPrivate = paramStr.StartsWith("?") || paramStr.StartsWith(">");
            if (table.TryHandle(finalChar, command, paramStr, out parameters, terminal, buffer, out var def))
            {
                if (def != null) this.LogDebug($"[CSI] {def.Description} → {string.Join(", ", parameters)}");
                else this.LogWarning($"[Handler] ESC[{paramStr}{command} är inte implementerad");
                // Här kan du trigga en TerminalAction eller uppdatera TerminalState
            }
            else
            {
                this.LogWarning($"[CsiSequenceHandler] Okänd sekvens ESC[{paramStr}{command}");
            }
        }
    }

    public class CsiCommandTable
    {
        // Metadata från JSON – nyckel = kommando, värde = definition
        internal readonly Dictionary<string, CsiCommandDefinition> _definitions;

        // Handlers för exekvering – nyckel = kommando, värde = Action
        private readonly Dictionary<string, Action<string[], TerminalState, ScreenBuffer>> _handlers;

        private readonly ModeManager _modeManager;
        private readonly VisualAttributeManager _visualAttributeManager;

        public CsiCommandTable(
            IEnumerable<CsiCommandDefinition> definitions,
            ModeManager modeManager,
            VisualAttributeManager visualAttributeManager,
            ScreenBuffer buffer,
            TerminalState terminal)
        {
            _modeManager = modeManager;
            _visualAttributeManager = visualAttributeManager;

            // Ladda metadata från JSON-listan
            _definitions = definitions.ToDictionary(
                d => $"{d.Command}:{d.Params}",   // unik nyckel
                d => d,
                StringComparer.Ordinal
            );

            // Bygg handler-tabellen
            _handlers = new(StringComparer.Ordinal)
            {
                ["H"] = (p, t, b) => b.SetCursorPosition(
                    ParseOrDefault(p.ElementAtOrDefault(0), 0),
                    ParseOrDefault(p.ElementAtOrDefault(1), 0)
                ),

                ["Y"] = (p, t, b) => b.SetCursorPosition(
                    b.CursorCol,
                    ParseOrDefault(p.ElementAtOrDefault(0), 0)
                ),

                ["h"] = (p, t, b) =>
                {
                    foreach (var mode in p.Where(x => x.StartsWith(">"))
                                          .Select(x => x.Substring(1))
                                          .Select(s => int.TryParse(s, out var m) ? m : (int?)null)
                                          .Where(m => m.HasValue))
                    {
                        _modeManager.Set(mode.Value);
                    }
                },

                ["l"] = (p, t, b) =>
                {
                    foreach (var mode in p.Where(x => x.StartsWith(">"))
                                          .Select(x => x.Substring(1))
                                          .Select(s => int.TryParse(s, out var m) ? m : (int?)null)
                                          .Where(m => m.HasValue))
                    {
                        _modeManager.Reset(mode.Value);
                    }
                },

                ["m"] = (p, t, b) =>
                {
                    _visualAttributeManager.HandleSGR(p, b, terminal);
                },

                ["r"] = (p, t, b) =>
                {
                    // PT200 CVD – Change Visual Attributes of Display
                    int scope = ParseOrDefault(p.ElementAtOrDefault(0), 2);
                    _visualAttributeManager.ChangeDisplayAttributes(scope, p.Skip(1).ToArray(), buffer, terminal);
                },
                ["K"] = (p, t, b) =>
                {
                    // PT200 Erase line command: 0 -> Cursor to EOL, 1 -> BOL to Cursor, 2 -> Entire line
                    // Current implententation supports only 0
                    b.ClearLine(ParseOrDefault(p.ElementAtOrDefault(0), 0));
                }
            };
        }

        /// <summary>
        /// Hämtar metadata för ett kommando om det finns.
        /// </summary>
        public bool TryGet(string command, string paramStr, out CsiCommandDefinition def)
        {
            // Försök med exakt nyckel först
            if (_definitions.TryGetValue($"{command}:{paramStr}", out def))
                return true;

            // Wildcard: matcha >nn eller >n
            if (paramStr.StartsWith(">"))
            {
                if (_definitions.TryGetValue($"{command}:>nn", out def) ||
                    _definitions.TryGetValue($"{command}:>n", out def))
                    return true;
            }

            // Fallback: matcha bara kommandot om det finns
            if (_definitions.TryGetValue(command, out def))
                return true;

            def = null;
            return false;
        }
        /// <summary>
        /// Försöker köra handlern för ett kommando.
        /// </summary>
        public bool TryHandle(char finalChar, string command, string paramStr, out string[] parameters, TerminalState terminal, ScreenBuffer buffer, out CsiCommandDefinition def)
        {
            def = null;
            parameters = (string.IsNullOrEmpty(paramStr)) ? Array.Empty<string>() : paramStr.Split(';').Select(p => p.Trim()).ToArray();

            if (IsRowColumn(paramStr) && _definitions.TryGetValue($"{command}:row;column", out def))
            {
                this.LogInformation($"[TryGet] {def.Name} -> {paramStr} {def.Description}, def={def.Name}");
            }
            else if (IsNumericList(paramStr) && _definitions.TryGetValue($"{command}:n1;n2;...", out def))
            {
                this.LogInformation($"[TryGet] {def.Name} -> {paramStr} {def.Description}, def={def.Name}");
            }
            else if (paramStr.StartsWith(">"))
            {
                var parts = paramStr.Split(';');

                if (parts.Length == 1 && parts[0].Length > 1 && parts[0][0] == '>' && parts[0].Substring(1).All(char.IsDigit))
                {
                    // Single parameter: >n eller >nn...
                    var digits = parts[0].Substring(1);

                    string key = $"{command}:>n";

                    if (_definitions.TryGetValue(key, out def))
                    {
                        this.LogInformation($"[TryHandle] {def.Name} -> {paramStr} {def.Description}");
                    }
                }
                else if (parts.All(p => p.Length > 1 && p[0] == '>' && p.Substring(1).All(char.IsDigit)))
                {
                    // Lista >n;>nn;>nnn...
                    if (_definitions.TryGetValue($"{command}:>n1;>n2;...", out def))
                    {
                        this.LogInformation($"[TryHandle] {def.Name} -> {paramStr} {def.Description}");
                    }
                }
            }
            else if (paramStr.All(char.IsDigit) && _definitions.TryGetValue($"{command}:n", out def))
            {
                if (def != null) this.LogInformation($"[TryHandle] {def.Name} -> {paramStr} {def.Description}, def={def.Name}");
            }

            if (_handlers.TryGetValue(command, out var handler))
            {
                handler(parameters, terminal, buffer);
                return true;
            }

            if (def == null && _definitions.TryGetValue(command, out var fallback))
                def = fallback;
            return false;
        }

        private static bool IsRowColumn(string paramStr)
        {
            var parts = paramStr.Split(';');
            return parts.Length == 2 &&
                   int.TryParse(parts[0], out _) &&
                   int.TryParse(parts[1], out _);
        }

        private static bool IsNumericList(string paramStr)
        {
            var parts = paramStr.Split(';');
            return parts.All(p => int.TryParse(p, out _));
        }

        private static int ParseOrDefault(string s, int def)
            => int.TryParse(s, out var val) ? val : def;
    }

    public class CsiCommandDefinition
    {
        public string Command { get; set; } = "";
        public string Name { get; set; } = "";
        public string Params { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class CsiCommandRoot
    {
        public List<CsiCommandDefinition> CSI { get; set; } = new();
    }
}