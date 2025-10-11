using Microsoft.Extensions.Logging;
using System;
using System.IO; // Viktigt för Path
using System.Text;
using System.Text.Json;
using Util;


namespace Parser
{
    public class TerminalParser
    {
        public TerminalParser() { }
        public void Feed(int data) { }
    }

    public class Buffer
    {
        public string[] Lines;
    }
}