using InputHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Transport;

namespace ConsoleTest
{
    public class ConsoleInputHandler : IInputHandler
    {
        private readonly IByteStream _stream;

        public ConsoleInputHandler(IByteStream stream)
        {
            _stream = stream;
        }

        public Task StartAsync(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var key = Console.ReadKey(intercept: true); // ingen lokal echo
                    var bytes = Encoding.ASCII.GetBytes(key.KeyChar.ToString());
                    await _stream.WriteAsync(bytes, token);
                }
            }, token);
        }
    }
}
