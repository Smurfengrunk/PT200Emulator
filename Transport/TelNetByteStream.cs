using PrimS.Telnet;
using Serilog;
using System.Text;

namespace Transport
{
    public class TelnetByteStream : IByteStream
    {
        private Client _client;
        private CancellationTokenSource _cts;
        private Task _receiveTask;

        public event Action<byte[]> DataReceived;
        public event Action Disconnected;

        public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            var tcpStream = new TcpByteStream(host, port);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _client = new Client(tcpStream, _cts.Token);
            await Task.Yield();
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _cts?.Cancel();
                _client?.Dispose();   // stänger streamen så ReadAsync kastar

                if (_receiveTask != null)
                {
                    try
                    {
                        await _receiveTask;
                    }
                    catch
                    {
                        /* ignorerar */
                    }
                }
            }
            finally
            {
                _client = null;
            }
        }

        public async Task WriteAsync(byte[] buffer, CancellationToken cancellationToken = default)
        {
            if (_client == null)
                throw new InvalidOperationException("Not connected.");

            var text = Encoding.ASCII.GetString(buffer);


            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _client.WriteAsync(text);
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"WriteAsync exception {ex}");
                OnDisconnected();
                return;
            }
        }

        public void Dispose() => DisconnectAsync().Wait();

        public Task StartReceiveLoop(CancellationToken token)
        {
            _receiveTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var response = await _client.ReadAsync();
                        if (response == null)
                        {
                            Log.Logger.Information("Servern stängde anslutningen.");
                            OnDisconnected();
                            break; // Avsluta loopen
                        }
                        else if (response.Length == 0)
                        {
                            await Task.Delay(50, token);
                            continue;
                        }
                        else if (response.Length >= 0)
                        {
                            var bytes = Encoding.ASCII.GetBytes(response);
                            DataReceived?.Invoke(bytes);
                        }
                        else throw new InvalidOperationException();
                    }
                    catch (OperationCanceledException)
                    {
                        OnDisconnected();
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error($"ReadAsync exception {ex}");
                        OnDisconnected();
                        break;
                    }
                }
            }, token);
            return _receiveTask;
        }

        private void OnDisconnected()
        {
            Log.Logger.Information("OnDisconnected() anropad");
            Disconnected?.Invoke();
        }
    }

    public class EchoFilter
    {
        private bool _lastSentWasBackspace;

        // Anropas från ConsoleAdapter när du skickar BS (0x08)
        public void MarkBackspaceSent()
        {
            _lastSentWasBackspace = true;
        }

        // Kör inkommande bytes genom filtret innan du skriver ut dem
        public IEnumerable<byte> FilterIncoming(IEnumerable<byte> incoming)
        {
            foreach (var b in incoming)
            {
                if (_lastSentWasBackspace && b == 0x20)
                {
                    // Ignorera serverns blankstegseko
                    _lastSentWasBackspace = false;
                    continue;
                }

                _lastSentWasBackspace = false;
                yield return b;
            }
        }
    }
}