using OverTCP.Messaging;
using System;
using System.Collections.Generic;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OverTCP.Dispatcher
{
    public class SingleThreadServer<T> : IDisposable where T : struct, Enum
    {
        Server mServer;
        ServerHook<T> mHook;
        public Server Server => mServer;
        public bool HasDisconnectedClients => mHook.HasDisconnectedClients;
        public bool HasConnectedClients => mHook.HasConnectedClients;
        public bool HasErrors => mHook.HasErrors;
        public bool HasRequests => mHook.HasRequests;
        public struct DataRequest
        {
            public T mType;
            public ulong mHeaderID;
            public ulong mSentByID;
            public byte[] mBytes;
        }

        public SingleThreadServer()
        {
            mServer = new Server();
            mHook = new(mServer);
        }

        public void Start(int port)
        {
            mServer.Start(port);
        }

        public void Stop()
        {
            Dispose();
        }

        public void SendToAll(DataRequest request)
        {
            SendToAll(request.mType, request.mBytes);
        }

        public void SendToAll(T type, ReadOnlySpan<byte> data)
        {
            mServer.SendToAll(Create.Data(type, Header.ALL_ULONG_ID, data));
        }

        public void SendTo(ulong destinationClientID, DataRequest request)
        {
            SendTo(destinationClientID, request.mType, request.mBytes);
        }

        public void SendTo(ulong destinationClientID, T type, ReadOnlySpan<byte> data)
        {
            mServer.SendTo(destinationClientID, Create.Data(type, Header.ALL_ULONG_ID, data));
        }

        public void SendToAllBut(DataRequest request, params ulong[] exceptIDs)
        {
            SendToAllBut(request.mType, request.mBytes, exceptIDs);
        }

        public void SendToAllBut(T type, ReadOnlySpan<byte> data, params ulong[] exceptIDs)
        {
            mServer.SendToAllBut(Create.Data(type, Header.ALL_ULONG_ID, data), exceptIDs);
        }

        public DataRequest[] GetRequests()
        {
            return mHook.GetRequests();
        }

        public Error[] GetErrors()
        {
            return mHook.GetErrors(); 
        }

        public ConnectedClient[] GetConnectedClients()
        {
            return mHook.GetConnectedClients();
        }

        public ulong[] GetDisconnectedClients()
        {
            return mHook.GetDisconnectedClients();
        }

        public void Dispose()
        {
            mHook.Dispose();
            mServer.Dispose();
        }
    }
}
