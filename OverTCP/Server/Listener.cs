using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace OverTCP
{
    internal class Listener : TcpListener
    {
        public Listener(IPEndPoint localEP) : base(localEP)
        {
        }

        public Listener(IPAddress localaddr, int port) 
            : base(localaddr, port)
        {
        }

        public bool IsActive => Active;
    }
}
