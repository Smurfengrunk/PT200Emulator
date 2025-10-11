using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PrimS.Telnet;

namespace Transport
{
    public class TelnetByteStream : IByteStream
    {
        private Client _client;
        private CancellationTokenSource _cts;

        public event Action<byte[]> DataReceived;

        public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            var tcpStream = new TcpByteStream(host, port);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _client = new Client(tcpStream, _cts.Token);

            // Starta en bakgrundsloop som lyssnar på inkommande data
            _ = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    var response = await _client.ReadAsync();
                    if (response != null)
                    {
                        var bytes = Encoding.ASCII.GetBytes(response);
                        DataReceived?.Invoke(bytes);
                    }
                }
            }, _cts.Token);

            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            _cts?.Cancel();
            _client?.Dispose();
            _client = null;
            return Task.CompletedTask;
        }

        public async Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (_client == null)
                throw new InvalidOperationException("Not connected.");

            var response = await _client.ReadAsync();
            return response != null
                ? Encoding.ASCII.GetBytes(response)
                : Array.Empty<byte>();
        }

        public async Task WriteAsync(byte[] buffer, CancellationToken cancellationToken = default)
        {
            if (_client == null)
                throw new InvalidOperationException("Not connected.");

            var text = Encoding.ASCII.GetString(buffer);
            if (!text.EndsWith("\r\n"))
                text += "\r\n";

            await _client.WriteAsync(text);
        }

        public void Dispose() => _client?.Dispose();
    }
}