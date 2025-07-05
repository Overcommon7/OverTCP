using OverTCP.Messaging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Text;

namespace OverTCP.Dispatcher
{
    public sealed class SingleThreadClient<T> : IDisposable where T : struct, Enum
    {
        public struct DataRequest
        {
            public T mType;
            public ulong mHeaderID;
            public Memory<byte> mBytes;

            public readonly Span<byte> Span => mBytes.Span;
        }

        Client mClient;
        ClientHook<T> mHook;
        public Client Client => mClient;

        public bool AnyExceptions => mHook.HasExceptions;
        public bool DidReconnect => mHook.DidReconnect;
        
        public SingleThreadClient() 
        { 
            mClient = new Client(16, false);
            mHook = new(mClient);
        }

        public bool Connect(IPAddress address, int port)
        {
            return mClient.Connect(address, port);
        }

        public void Reconnect()
        {
            mClient.Reconnect();
        }

        public void SendData(DataRequest data)
        {
            mClient.SendData(Create.Data(data.mType, data.mHeaderID, data.mBytes.Span));
        }

        public void SendData(T type, ReadOnlySpan<byte> bytes)
        {
            mClient.SendData(Create.Data(type, mClient.ID, bytes));
        }

        public void SendData(T type, string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            mClient.SendData(Create.Data(type, mClient.ID, bytes));
        }

        public void SendData(T type, ulong headerID, ReadOnlySpan<byte> bytes)
        {
            mClient.SendData(Create.Data(type, headerID, bytes));
        }

        public DataRequest[] GetDataRequests()
        {
            mClient.FreeAllocatedArrays();
            return mHook.GetRequests();
        }

        public Exception[] GetExceptions()
        {
            return mHook.GetExceptions(); 
        }

        public void Disconnect()
        {
            Dispose();
        }

        public void Dispose()
        {
            mHook.Dispose();
            mClient.Dispose();
        }
    }
}
