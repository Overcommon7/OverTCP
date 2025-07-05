using System;
using System.Buffers;
using System.Net.Sockets;
using System.Threading;

namespace OverTCP
{
    internal enum ReadCode
    {
        ServerDisconnected = -1000,
        Error = -1,

        AllDataRead = 20,
        ReadSuccesful = 100        
    }
    internal static class IncomingDataHandler
    {
        internal const int HEADER_SIZE = sizeof(int);

        internal static (ReadCode mReadCode, bool mIsRentedFromPool, int mSize) HandleFromClient(TcpClient client, out byte[]? data, out Exception? exception)
        {
            Socket socket = client.Client;
            exception = null;
            if (socket is null || socket.Connected == false)
            {
                data = null;
                exception = new Exception("Socket Is No Longer Connected");
                return (ReadCode.Error, false, 0);
            }

            if (socket.Available < HEADER_SIZE)
            {
                data = null;
                exception = null;
                return (ReadCode.AllDataRead, false, 0);
            }

            NetworkStream? stream = null;
            int size = 0;         
            try
            {
                stream = client.GetStream();
                var buffer = new byte[HEADER_SIZE];
                StreamHandling.Read(stream, HEADER_SIZE, buffer);
                size = BitConverter.ToInt32(buffer);
            }
            catch (Exception e)
            {
                data = null;
                Log.Error("Could Not Read Header: "  + e.Message);
                exception = e;
                return (ReadCode.Error, false, 0);
            }

            if (size == int.MaxValue)
            {
                data = BitConverter.GetBytes(int.MaxValue);
                return (ReadCode.ServerDisconnected, false, 0);
            }

            if (stream is null)
            {
                data = null;
                Log.Error("Stream Was Null");
                exception = new Exception("Stream Was Null");
                return (ReadCode.Error, false, 0);
            }   
            
            if (size <= 0)
            {
                data = null;
                Log.Error($"Size Of Header Was Posted As {size}");
                exception = new Exception($"Size Of Header Was Posted As {size}");
                return (ReadCode.Error, false, 0);
            }

            try
            {        
                if (size > short.MaxValue)
                {
                    data = ArrayPool<byte>.Shared.Rent(size);
                }
                else
                {
                    data = new byte[size];
                }

                StreamHandling.Read(stream, size, data);                               
                return (ReadCode.ReadSuccesful, size > short.MaxValue, size);
            }
            catch (Exception e)
            {
                exception = e;
                Log.Error(e.Message);                
            }
            
            data = null;
            return (ReadCode.Error, false, 0);
        }
    }
}
