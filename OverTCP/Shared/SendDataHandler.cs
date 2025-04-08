using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace OverTCP
{
    internal static class SendDataHandler
    {
        public static bool SendData(TcpClient client, ReadOnlySpan<byte> data, [NotNullWhen(false)] out Exception? exception)
        {            
            int retryCount = 3;
            exception = null;
            while (retryCount > 0)
            {
                try
                {
                    int length = data.Length;
                    var stream = client.GetStream();
                    stream.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<int>(ref length)));
                    stream.Write(data);
                    break;
                }
                catch (Exception e)
                {
                    exception = e;
                    --retryCount;
                    Thread.Sleep(150);
                }
            }

            if (retryCount <= 0)
            {
                if (exception is null)
                    exception = new Exception();
                Log.Error(exception.Message);
                return false;
            }

            return true;
        }
    }
}
