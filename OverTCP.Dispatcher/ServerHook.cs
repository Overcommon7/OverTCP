using OverTCP.Messaging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;

namespace OverTCP.Dispatcher
{
    public record Error(Exception exception, ulong clientID);
    public record ConnectedClient(ulong clientID, TcpClient client);
    internal class ServerHook<T> : IDisposable where T : struct, Enum
    {
        ConcurrentBag<SingleThreadServer<T>.DataRequest> mRequests;
        List<Error> mErrors;
        List<ulong> mDisconnectedClients;
        List<ConnectedClient> mConnectedClients;
        Server mServer;

        bool mDisposed = false;
        internal bool HasDisconnectedClients => mDisconnectedClients.Count > 0;
        internal bool HasConnectedClients => mConnectedClients.Count > 0;
        internal bool HasErrors => mErrors.Count > 0;
        internal bool HasRequests => mRequests.Count > 0;
        internal ServerHook(Server server)
        {
            mServer = server;
            mRequests = new();
            mErrors = new();
            mDisconnectedClients = new();
            mConnectedClients = new();

            mServer.OnClientConnected += Server_OnClientConnected;
            mServer.OnClientDisconnected += Server_OnClientDisconnected;
            mServer.OnDataRecieved += Server_OnDataRecieved;
            mServer.OnErrorThrown_Read += Server_OnErrorThrown_Read;
            mServer.OnErrorThrown_Write += Server_OnErrorThrown_Write;
        }

        private void Server_OnErrorThrown_Write(ulong clientID, Exception exception)
        {
            lock(mErrors)
                mErrors.Add(new(exception, clientID));
        }

        private void Server_OnErrorThrown_Read(ulong clientID, Exception exception)
        {
            lock (mErrors)
                mErrors.Add(new(exception, clientID));
        }

        private void Server_OnDataRecieved(ulong clientID, byte[] data)
        {
            SingleThreadServer<T>.DataRequest request = new();
            request.mBytes = Extract.All(data, out request.mType, out request.mHeaderID).ToArray();
            request.mSentByID = clientID;
            mRequests.Add(request);
        }

        private void Server_OnClientDisconnected(ulong clientID)
        {
            lock (mDisconnectedClients)
                mDisconnectedClients.Add(clientID);
        }

        private void Server_OnClientConnected(ulong clientID, TcpClient client)
        {
            lock (mConnectedClients)
                mConnectedClients.Add(new(clientID, client));
        }

        internal SingleThreadServer<T>.DataRequest[] GetRequests()
        {
            if (mRequests.Count == 0)
                return [];

            SingleThreadServer<T>.DataRequest[] requests;
            requests = mRequests.ToArray();
            mRequests.Clear();
            return requests;
        }

        public Error[] GetErrors()
        {
            if (mErrors.Count == 0)
                return [];

            Error[] errors;
            lock (mErrors)
            {
                errors = mErrors.ToArray();
                mErrors.Clear();
            }

            return errors;
        }

        public ConnectedClient[] GetConnectedClients()
        {
            if (mErrors.Count == 0)
                return [];

            ConnectedClient[] connectedClients;
            lock (mErrors)
            {
                connectedClients = mConnectedClients.ToArray();
                mDisconnectedClients.Clear();
            }

            return connectedClients;
        }

        public ulong[] GetDisconnectedClients()
        {
            if (mErrors.Count == 0)
                return [];

            ulong[] disconnectedClients;
            lock (mErrors)
            {
                disconnectedClients = mDisconnectedClients.ToArray();
                mDisconnectedClients.Clear();
            }

            return disconnectedClients;
        }

        public void Dispose()
        {
            if (mDisposed) 
                return;

            mDisposed = true;
            mServer.OnClientConnected -= Server_OnClientConnected;
            mServer.OnClientDisconnected -= Server_OnClientDisconnected;
            mServer.OnDataRecieved -= Server_OnDataRecieved;
            mServer.OnErrorThrown_Read -= Server_OnErrorThrown_Read;
            mServer.OnErrorThrown_Write -= Server_OnErrorThrown_Write;
        }
    }
}
