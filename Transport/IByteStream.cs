using System;
using System.Threading;
using System.Threading.Tasks;

namespace Transport
{
    public interface IByteStream : IDisposable
    {
        /// <summary>
        /// Etablerar en anslutning till given host och port.
        /// </summary>
        Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Kopplar ned anslutningen.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Läser inkommande data som råa bytes.
        /// </summary>
        Task<byte[]> ReadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Skickar data som råa bytes.
        /// </summary>
        Task WriteAsync(byte[] buffer, CancellationToken cancellationToken = default);

        ///<summary>
        ///Event för att slippa loopa i onädan
        ///</summary>
        public event Action<byte[]> DataReceived;
    }
}