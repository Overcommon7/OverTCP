using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OverTCP
{
    internal record ConnectedClient(TcpClient Client, EndPoint EndPoint, ulong ID);

    public static class ClientExtenstions
    {
        public static ulong GetID(this Client client) => client.TCPClient?.GetID() ?? 0;
        public static ulong GetID(this TcpClient client) => client.Client.RemoteEndPoint.GetID();
        public static ulong GetID(this EndPoint? endPoint)
        {
            if (endPoint is null)
                return 0;

            string? text = endPoint.ToString();  
            return HashString(text);
        }

        public static ulong HashString(string? text)
        {
            const ulong FNVOffsetBasis64 = 14695981039346656037;
            const ulong FNVPrime64 = 1099511628211;

            if (string.IsNullOrEmpty(text))
                return 0;

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            ulong hash = FNVOffsetBasis64;

            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= FNVPrime64;
            }

            return hash;
        }
    }
    
}
