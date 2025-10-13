using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InputHandler
{
    public interface IInputHandler
    {
        Task StartAsync(CancellationToken cancellationToken);
    }
}
