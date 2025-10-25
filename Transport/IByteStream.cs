namespace Transport
{
    public interface IByteStream : IDisposable
    {
        /// <summary>
        /// Etablerar en anslutning till given host och port.
        /// </summary>
        Task<bool> ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Kopplar ned anslutningen.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Startar vänteloop för data från servern.
        /// </summary>
        Task StartReceiveLoop(CancellationToken cancellationToken = default);

        /// <summary>
        /// Skickar data som råa bytes.
        /// </summary>
        Task WriteAsync(byte[] buffer, CancellationToken cancellationToken = default);

        ///<summary>
        ///Event för att slippa loopa i onädan
        ///</summary>
        public event Action<byte[]> DataReceived;
        public event Action Disconnected;
    }
}