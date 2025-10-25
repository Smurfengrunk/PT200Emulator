using PrimS.Telnet;
using Serilog;
using System.Text;

namespace Transport
{
    public class TelnetByteStream : IByteStream, IAsyncDisposable
    {
        private Client _client;
        private CancellationTokenSource _cts;
        private Task _receiveTask;

        public event Action<byte[]> DataReceived;
        public event Action Disconnected;
        private bool _disconnected = false;

        public async Task<bool> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            try
            {
                var tcpStream = new TcpByteStream(host, port);
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _client = new Client(tcpStream, _cts.Token);
                await Task.Yield();
                _disconnected = false;
            }
            catch
            {
                OnDisconnected();
                return false;
            }
            return true;
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

        public Task StartReceiveLoop(CancellationToken token)
        {
            string response = null;
            _receiveTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (_client != null) response = await _client.ReadAsync();
                        if (response == null)
                        {
                            Log.Logger.Error("Servern stängde anslutningen.");
                            OnDisconnected();
                            break; // Avsluta loopen
                        }
                        else if (response.Length == 0)
                        {
                            await Task.Delay(50, token);
                            continue;
                        }
                        else
                        {
                            var bytes = Encoding.ASCII.GetBytes(response);
                            DataReceived?.Invoke(bytes);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        Log.Logger.Error("Invalid Operation Exception");
                        OnDisconnected();
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Logger.Error("Operation Cancelled Exception");
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
            if (!_disconnected)
            {
                Log.Logger.Error($"Disconnected event triggered");
                Disconnected?.Invoke();
                _disconnected = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
        }

        // Behåll ev. IDisposable också om du vill vara bakåtkompatibel
        public void Dispose()
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }

    }
}