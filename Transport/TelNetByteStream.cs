using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PrimS.Telnet;
using Serilog;
using Util;

namespace Transport
{
    public class TelnetByteStream : IByteStream
    {
        private Client _client;
        private CancellationTokenSource _cts, _rcts;
        private Task _receiveTask;

        public event Action<byte[]> DataReceived;

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

        public async Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
        {
            _rcts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (_client == null)
                throw new InvalidOperationException("Not connected.");

            cancellationToken.ThrowIfCancellationRequested();
            var response = await _client.ReadAsync();
            this.LogDebug($"Readasync response is {response}");
            return response != null
                ? Encoding.ASCII.GetBytes(response)
                : Array.Empty<byte>();
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
                //Log.Logger.LogDebug($"Skickar {buffer.Length} bytes: \"{text}\"");
            }
            catch (Exception ex)
            {
                Log.Logger.LogErr($"WriteAsync exception {ex}");
                throw;
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
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.LogErr($"ReadAsync exception {ex}");
                        break;
                    }
                }
            }, token);
            return _receiveTask;
        }

    }
}