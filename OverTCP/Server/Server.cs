using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace OverTCP
{
    public class Server : IDisposable
    {
        const int POLLING_SCALING_FACTOR = 2;

        List<ConnectedClient> mClients;
        ConnectionHandler mConnectionHandler;
        Thread mDataPollingThread;
        bool mIsServerRunning;
        bool mIsDisposed = false;
        int mPollingInterval;
        (int, ulong)[] mDisconnectedClients = new (int, ulong)[5];

        public delegate void DataRecieved(ulong clientID, byte[] data);
        public delegate void ClientConnected(ulong clientID, TcpClient client);
        public delegate void ClientDisconnected(ulong clientID);
        public delegate void ErrorThrown(ulong clientID, Exception exception);

        public event DataRecieved? OnDataRecieved;
        public event ClientConnected? OnClientConnected;
        public event ClientDisconnected? OnClientDisconnected;
        public event Action? OnServerShutdown;
        public event ErrorThrown? OnErrorThrown_Read;
        public event ErrorThrown? OnErrorThrown_Write;


        public int ClientCount => mClients.Count;
        public int Port { get; private set; } = 0;
        public IPAddress? IPAddress { get; private set; } 
        public Server(int pollingInterval = 10) 
        { 
            mClients = new List<ConnectedClient>();
            mConnectionHandler = new ConnectionHandler();
            mDataPollingThread = new Thread(DataPolling);
            mDataPollingThread.IsBackground = true;
            mIsServerRunning = false;
            mPollingInterval = pollingInterval;
        }

        /// <summary>
        /// Start The Server On The Given Port<br></br>Automatically Enables Allowing New Connections.
        /// </summary>
        /// <param name="port"></param>
        public void Start(int port = 25565)
        {
            Port = port;

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            IPAddress = ip.Address;
                            break;
                        }
                    }
                }

                if (IPAddress is not null)
                    break;
            }

            if (IPAddress is null)
            {
                Log.Error("Could Not Find Active Network Adapter To Start Server");
                return;
            }

            Log.Message($"Server Created -> {IPAddress}");
            mIsServerRunning = true;
            EnableNewConnections();
            mDataPollingThread.Start();
        }

        public void DisableNewConnections()
        {
            mConnectionHandler.Stop();
            mConnectionHandler.OnClientConnected -= ConnectionHandler_OnClientConnected;
        }

        public void EnableNewConnections()
        {
            if (IPAddress is not null)
            {
                mConnectionHandler.OnClientConnected += ConnectionHandler_OnClientConnected;
                mConnectionHandler.Start(IPAddress, Port);
            }
               
        }

        public void SendTo(ulong clientID, byte[] data)
        {
            int index = mClients.FindIndex(client => client.ID == clientID);
            if (index == -1)
            {
                Log.Error($"{clientID} Is An Invalid Client ID");
                return;
            }

            if (!SendDataHandler.SendData(mClients[index].Client, data, out var exception))
                OnErrorThrown_Write?.Invoke(clientID, exception);
        }

        public void SendToAll(byte[] data)
        {
            for (int i = 0; i < mClients.Count; ++i)
            {
                var client = mClients[i];
                if (!SendDataHandler.SendData(client.Client, data, out var exception))
                {
                    OnErrorThrown_Write?.Invoke(client.ID, exception);
                }                    
            }                
        }

        public void SendToAllBut(byte[] data, ulong excludingID)
        {
            for (int i = 0; i < mClients.Count; ++i)
            {
                var client = mClients[i];
                if (client.ID != excludingID)
                {
                    if (!SendDataHandler.SendData(client.Client, data, out var exception))
                    {
                        OnErrorThrown_Write?.Invoke(client.ID, exception);
                    }                       
                }                    
            }
        }
        public void SendToAllBut(byte[] data, params ulong[] excludingIDs)
        {
            for (int i = 0; i < mClients.Count; ++i)
            {
                var client = mClients[i];
                if (Array.FindIndex(excludingIDs, id => id == client.ID) == -1)
                {
                    if (!SendDataHandler.SendData(client.Client, data, out var exception))
                    {
                        OnErrorThrown_Write?.Invoke(client.ID, exception);
                    }
                }                    
            }                
        }

        private void DataPolling()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (mIsServerRunning)
            {    
                stopwatch.Restart();
                int disconeectedCount = 0;
                int index = 0; 
                for (int i = 0; i < mClients.Count; ++i)
                {
                    var client = mClients[i];
                    if (!IsClientConnected(client))
                    {
                        Log.Message($"Client Disconnected: {client.ID}");
                        mDisconnectedClients[disconeectedCount] = (index, client.ID);
                        ++disconeectedCount;
                    }
                    ++index;
                }

                for (int i = 0; i < disconeectedCount; ++i)
                {
                    OnClientDisconnected?.Invoke(mDisconnectedClients[i].Item2);
                }

                lock (mDisconnectedClients)
                {
                    for (int i = 0; i < disconeectedCount; ++i)
                    {
                        mClients.RemoveAt(mDisconnectedClients[i].Item1);
                    }
                }

                for (int i = 0; i < mClients.Count; ++i)
                {
                    var client = mClients[i];
                    while (true)
                    { 
                        var code = IncomingDataHandler.HandleFromClient(client.Client, out var data, out var exception);
                        if (code == ReadCode.ReadSuccesful && data is not null)
                            OnDataRecieved?.Invoke(client.ID, data);
                        else if (code == ReadCode.AllDataRead)
                            break;
                        else if (code == ReadCode.Error)
                        {
                            OnErrorThrown_Read?.Invoke(client.ID, exception ?? new Exception());
                            break;
                        }
                    }            
                }

                int waitTime = mPollingInterval - (int)stopwatch.ElapsedMilliseconds;
                if (waitTime > 0)
                    Thread.Sleep(waitTime);
            }
        }

        private void ConnectionHandler_OnClientConnected(ConnectedClient client)
        {
            lock (mClients)
                mClients.Add(client);

            Log.Message(client.ID);
            client.Client.GetStream().Write(BitConverter.GetBytes(client.ID));
            OnClientConnected?.Invoke(client.ID, client.Client);
        }

        private bool IsClientConnected(ConnectedClient client)
        {
            Socket socket = client.Client.Client;

            if (!socket.Connected)
                return false;

            bool part1 = socket.Poll(1000 / POLLING_SCALING_FACTOR, SelectMode.SelectRead);
            bool part2 = socket.Available == 0;

            if (part1 && part2)
                return false;

            return true;
        }

        /// <summary>
        /// Stops The Server And Disconnects Any Connected Clients.
        /// </summary>
        public void Stop() => Dispose();
        ~Server() => Dispose();
        public void Dispose()
        {
            if (mIsDisposed)
                return;
            OnServerShutdown?.Invoke();

            //Send The Disconnect Code To Clients
            ulong maxUValue = ulong.MaxValue;
            int maxIValue = int.MaxValue;
            ReadOnlySpan<byte> data;
            if (IncomingDataHandler.HEADER_SIZE == 4)
            {
                data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref maxIValue, 1));
            }
            else if (IncomingDataHandler.HEADER_SIZE == 8)
            {
                data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref maxUValue, 1));
            }

            if (data.Length != 0)
            {
                for (int i = 0; i < mClients.Count; ++i)
                {
                    mClients[i].Client.GetStream().Write(data);
                }
            }
            

            mIsDisposed = true;
            mIsServerRunning = false;
            DisableNewConnections();
            mDataPollingThread.Join();

            for (int i = 0; i < mClients.Count; ++i)
            {
                mClients[i].Client.Close(); 
            }
                
        }
    }
}
