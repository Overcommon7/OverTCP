using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace OverTCP
{
    internal static class StreamHandling
    {
        internal static byte[] Read(NetworkStream stream, int count)
        {
            byte[] buffer = new byte[count];
            int bytesRead = 0, prevRollingCount = 0, retryCount = 0, rollingCount = count;
            while (true)
            {
                prevRollingCount = rollingCount;               
                int read = stream.Read(buffer, bytesRead, rollingCount);
                bytesRead += read;
                rollingCount -= read;

                if (bytesRead >= count)
                    return buffer;

                if (read < 1)
                    ++retryCount;
                else
                    retryCount = 0;

                if (retryCount >= 10)
                {
                    throw new Exception($"Could Not Fetch {count} Bytes Of Data From Stream {stream.Socket.RemoteEndPoint}");
                }

                Thread.Sleep(10);
            }
        }
    }
}
