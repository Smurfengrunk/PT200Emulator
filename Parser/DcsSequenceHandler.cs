using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Util;

namespace Parser
{

    public class DcsSequenceHandler
    {
        //public event Action<string, IReadOnlyList<TerminalAction>> ActionsReady;
        public event Action<byte[]> OnDcsResponse;
        public event Action<string> OnStatusUpdate;

        private readonly TerminalState state;
        private readonly string jsonPath;

        public DcsSequenceHandler(TerminalState state, string jsonPath)
        {
            this.state = state;
            this.jsonPath = jsonPath;
        }

        public void Handle(byte[] payload)
        {
            RaiseStatus("🟡 Väntar på DCS");
            if (payload.Length == 0)
            {
                this.LogDebug("[DCS] Tom DCS mottagen – statusförfrågan.");
                RaiseStatus("🟡 Väntar på DCS");
                var dcs = state.BuildDcs(this.jsonPath);
                SendDcsResponse(dcs);
                return;
            }

            var content = Encoding.ASCII.GetString(payload);
            this.LogDebug($"[DCS] Innehåll: {content}");
            this.LogDebug($"[DCS] Payload: {BitConverter.ToString(payload)}");
            this.LogDebug($"[DCS] Tolkat innehåll: {content}");

            state.ReadDcs(this.jsonPath, content);
            var actions = DcsSequenceHandler.Build(content);
            //ActionsReady?.Invoke(actions);
            state.DisplayDCS();
        }

        private void SendDcsResponse(string dcs)
        {
            this.LogDebug($"[DCS] OnDcsResponse is {(OnDcsResponse == null ? "null" : "set")}");
            var hex = BitConverter.ToString(Encoding.ASCII.GetBytes(dcs));
            this.LogDebug($"[DCS Response HEX] {hex}, Längd = {dcs.Length}");
            var bytes = Encoding.ASCII.GetBytes(dcs);
            this.LogTrace($"[DCS] Using handler hash={this.GetHashCode()}");
            OnDcsResponse?.Invoke(bytes);
        }
        public static IReadOnlyList<TerminalAction> Build(string content)
        {
            var actions = new List<TerminalAction>();

            if (content.Contains("BLOCK", StringComparison.OrdinalIgnoreCase))
                actions.Add(new SetModeAction("BLOCK"));
            else if (content.Contains("LINE", StringComparison.OrdinalIgnoreCase))
                actions.Add(new SetModeAction("LINE"));

            actions.Add(new DcsAction(content));
            return actions;
        }
        private void RaiseStatus(string message)
        {
            OnStatusUpdate?.Invoke(message);
        }
    }
}
