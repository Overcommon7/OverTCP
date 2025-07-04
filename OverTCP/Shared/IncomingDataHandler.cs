using System;
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

        internal static ReadCode HandleFromClient(TcpClient client, out byte[]? data, out Exception? exception)
        {
            Socket socket = client.Client;
            exception = null;
            if (socket is null || socket.Connected == false)
            {
                data = null;
                exception = new Exception("Socket Is No Longer Connected");
                return ReadCode.Error;
            }

            if (socket.Available < HEADER_SIZE)
            {
                data = null;
                exception = null;
                return ReadCode.AllDataRead;
            }

            NetworkStream? stream = null;
            int size = 0;         
            try
            {
                stream = client.GetStream();
                var buffer = StreamHandling.Read(stream, HEADER_SIZE);
                size = BitConverter.ToInt32(buffer);
            }
            catch (Exception e)
            {
                data = null;
                Log.Error("Could Not Read Header: "  + e.Message);
                exception = e;
                return ReadCode.Error;
            }

            if (size == int.MaxValue)
            {
                data = BitConverter.GetBytes(int.MaxValue);
                return ReadCode.ServerDisconnected;
            }

            if (stream is null)
            {
                data = null;
                Log.Error("Stream Was Null");
                exception = new Exception("Stream Was Null");
                return ReadCode.Error;
            }   
            
            if (size <= 0)
            {
                data = null;
                Log.Error($"Size Of Header Was Posted As {size}");
                exception = new Exception($"Size Of Header Was Posted As {size}");
                return ReadCode.Error;
            }

            try
            {                                    
                data = StreamHandling.Read(stream, size);                               
                return ReadCode.ReadSuccesful;
            }
            catch (Exception e)
            {
                exception = e;
                Log.Error(e.Message);                
            }
            
            data = null;
            return ReadCode.Error;
        }
    }
}
