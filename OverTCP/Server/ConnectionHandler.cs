using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace OverTCP
{
    internal class ConnectionHandler
    {
        Thread mConnectionThread;
        Listener? mListener;

        internal delegate void ClientConnected(ConnectedClient client);
        internal event ClientConnected? OnClientConnected;
        internal ConnectionHandler()
        {
            mConnectionThread = new Thread(HandleIncomingConnections);
        }
        internal void Start(IPAddress address, int port)
        {
            mListener = new Listener(address, port);
            mListener.Start();
            mListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            mConnectionThread.Start();
        }

        internal void Stop()
        {
            mListener?.Stop();
            mConnectionThread.Join();
        }

        private void HandleIncomingConnections()
        {
            while (mListener is not null && mListener.IsActive)
            {
                while (mListener.Pending())
                {
                    TcpClient client = mListener.AcceptTcpClient();
                    EndPoint? clientEndpoint = client.Client.RemoteEndPoint;
                    if (clientEndpoint is null)
                    {
                        Log.Error("Client Trying To Connect Had An Ivalid Endpoint");
                        continue;
                    }

                    ConnectedClient connectedClient = new(client, clientEndpoint, clientEndpoint.GetID());
                    Log.Message($"Client Connected: {clientEndpoint} ID: {connectedClient.ID}");
                    OnClientConnected?.Invoke(connectedClient);
                }

                Thread.Sleep(50);
            }
        }
    }
}
