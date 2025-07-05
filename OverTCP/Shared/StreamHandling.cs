using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace OverTCP
{
    internal static class StreamHandling
    {
        const int RETRIES_TILL_FAIL = 10;
        internal static void Read(NetworkStream stream, int count, byte[] buffer)
        {
            int bytesRead = 0, retryCount = 0, rollingCount = count;
            while (true)
            {         
                int read = stream.Read(buffer, bytesRead, rollingCount);
                bytesRead += read;
                rollingCount -= read;

                if (bytesRead >= count)
                    return;

                if (read < 1)
                    ++retryCount;
                else
                    retryCount = 0;

                if (retryCount >= RETRIES_TILL_FAIL)
                {
                    throw new Exception($"Could Not Fetch {count} Bytes Of Data From Stream {stream.Socket.RemoteEndPoint}");
                }

                Thread.Sleep(10);
            }
        }
    }
}
