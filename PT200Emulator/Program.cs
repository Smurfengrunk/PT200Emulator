using InputHandler;
using Parser;
using Rendering;
using Serilog;
using System;
using System.Text;
using Transport;

namespace PT200Emulator
{
#pragma warning disable CS8618
    internal class Program
    {
        static IByteStream _stream = new TelnetByteStream();
        public static CancellationTokenSource cts { get; set; } = new CancellationTokenSource();
        public static CancellationTokenSource rcts { get; set; } = new CancellationTokenSource();
        private static TerminalState _state = new();
        private static DataPathProvider _basePath = new(AppDomain.CurrentDomain.BaseDirectory);
        private static LocalizationProvider _localization = new();
        private static ModeManager modeManager = new ModeManager(_localization);
        private static TerminalControl _terminal = new();
        private static TerminalParser _parser;
        private static ConsoleRenderer renderer = new();
        private static IInputMapper _mapper;
        private static async Task Main(string[] args)
        {
            _state.screenFormat = TerminalState.ScreenFormat.S80x24;
            _state.SetScreenFormat();
            _parser = new TerminalParser(_basePath, _state, modeManager, _terminal);
            _mapper = new InputHandler.InputHandler().inputMapper;
            var adapter = new ConsoleAdapter(_mapper, bytes => _stream.WriteAsync(bytes));
            ConfigureLogging();

            _parser.Screenbuffer.BufferUpdated += () => renderer.Render(_parser.Screenbuffer, _parser.inEmacs);
            _parser.DcsResponse += (bytes) => _stream.WriteAsync(bytes);
            _parser.Screenbuffer.Scrolled += () => renderer.ForceFullRender();
            _parser.Screenbuffer.AttachCaretController(new ConsoleCaretController());

            _stream.Disconnected += () =>
            {
                Log.Information("Servern kopplade ner, stänger programmet.");
                _ = ShutdownAsync();
            };

            if (!await Connect(cts.Token)) return;
            adapter.Run();

            Console.WriteLine("Tryck Ctrl+C för att avsluta.");
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (TaskCanceledException) { _ = ShutdownAsync(); }
        }

        public static async Task<bool> Connect(CancellationToken cancellationToken, string host = "localhost", int port = 2323)
        {
            try
            {
                await _stream.ConnectAsync(host, port, cts.Token);
                _stream.DataReceived += bytes => _parser.Feed(bytes);
                _ = _stream.StartReceiveLoop(rcts.Token);
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"Anslutning misslyckades: {ex}");
                return false;
            }
            return true;
        }

        public static async Task Disconnect()
        {
            try
            {
                var disconnectTask = _stream.DisconnectAsync();
                var completed = await Task.WhenAny(disconnectTask, Task.Delay(2000));
                if (completed != disconnectTask)
                    Log.Logger.Warning("Disconnect hängde, fortsätter ändå...");
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"Frånkoppling misslyckades: {ex}");
            }
        }

        public static async Task<bool> Send(string text, CancellationToken cancellationToken)
        {
            if (_stream == null) return false;

            try
            {
                var bytes = Encoding.ASCII.GetBytes(text);
                await _stream.WriteAsync(bytes, cancellationToken);
                Log.Logger.Debug($"Skickade {bytes.Length} bytes: \"{text}\"");
                return true;
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"Sändning misslyckades: {ex}");
                return false;
            }
        }

        public static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug()    // VS Debug‑fönstret
                                    //.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}") // parallellt till konsolen
                .CreateLogger();
        }

        public static async Task ShutdownAsync()
        {
            Log.Logger.Information("Servern stängde sessionen – avslutar om 2 sekunder...");
            await Task.Delay(2000);

            Log.Logger.Debug("Kopplar ner...");
            await Disconnect();

            Log.Logger.Debug("Avslutar");
            cts.Cancel();
            Environment.Exit(0);
        }
#pragma warning restore CS8618
    }
}