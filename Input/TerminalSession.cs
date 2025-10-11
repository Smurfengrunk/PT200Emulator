using PT200Emulator.Core.Emulator;
using PT200Emulator.Core.Input;
using PT200Emulator.Core.Parser;
using PT200Emulator.Core.Routing;
using PT200Emulator.Infrastructure.Logging;
using PT200Emulator.UI;
using System;
using System.Diagnostics.Metrics;
using System.IO;
using System.Text;

namespace PT200Emulator.Core.Input
{
    public class TerminalSession
    {
        public InputController Controller { get; }
        public IInputMapper Mapper { get; }
        public string TerminalId { get; set; } = "PT200 #01";
        public int BaudRate { get; set; } = 9600;

        internal readonly ITerminalParser _parser;
        //private readonly TerminalState state;
        private readonly CommandRouter router;
        internal readonly TerminalState _state;
        public TerminalState.DisplayType DisplayTheme { get; set; }
        public ScreenBuffer ScreenBuffer => _state.ScreenBuffer;

        public TerminalSession(InputController controller, IInputMapper mapper, string basePath, TerminalState state, ITerminalParser parser)
        {
            // Minimal synkron setup
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _parser = parser;

            if (string.IsNullOrWhiteSpace(basePath))
                throw new ArgumentException("Base path is required.", nameof(basePath));

            var provider = new DataPathProvider(basePath);
            router = new CommandRouter(controller, state);

            DisplayTheme = _state.Display;
            this.LogDebug($"[TERMINALSESSION] Hashcode: {this.GetHashCode()}");

        }

#pragma warning disable CS1998 // Because this call is not awaited
        public async Task InitializeAsync()
        {
            _parser.OnDcsResponse += data =>
            {
                this.LogTrace($"[INITIALIZEASYNC/ONDCSRESPONSE] data = {new string(Encoding.ASCII.GetChars(data))}, Controller = {Controller.GetHashCode()}");
                _ = Controller.SendRawAsync(data)
                    .ContinueWith(t => this.LogError($"Fel vid DCS-sändning: {t.Exception}"),
                                  TaskContinuationOptions.OnlyOnFaulted);
            };

            _parser.ActionsReady += async actions =>
            {
                this.LogTrace($"[Session] {actions.Count} action(s) mottagna");
                await router.Route(actions);
            };

            return;
        }
#pragma warning restore CS1998
    }
}