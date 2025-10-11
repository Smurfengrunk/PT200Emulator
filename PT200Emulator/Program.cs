using Parser;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Transport;

namespace PT200Emulator
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var parser = new TerminalParser();

            using IByteStream stream = new TelnetByteStream();
            stream.DataReceived += parser.Feed;

            await stream.ConnectAsync("localhost", 2323, CancellationToken.None);

            Console.WriteLine("Ansluten. Tryck Enter för att avsluta.");
            Console.ReadLine();

            await stream.DisconnectAsync();
        }
    }

    // Parser-skal
    public class TerminalParser
    {
        public void Feed(byte[] data)
        {
            if (data == null || data.Length == 0)
                return; // hoppa över tomma paket

            Console.WriteLine($"[Parser] Fick {data.Length} bytes");
            Console.WriteLine(Encoding.ASCII.GetString(data));
        }
    }
}
