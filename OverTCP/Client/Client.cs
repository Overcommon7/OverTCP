using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Windows.Devices.Display.Core;
using Windows.Services.Maps;

namespace OverTCP
{
    public class Client : IDisposable
    {
        public delegate void DataRecieved(byte[] data);
        public event DataRecieved? OnDataRecieved;
        public event Action? OnServerClosed;
        public event Action<Exception>? OnErrorPosted;
        public event Action? OnReconnected;
        public TcpClient? TCPClient { get; private set; }
        Thread mPollingThread;
        bool mIsPolling;
        int mPollingInterval;
        bool mIsDisposed = false;
        ulong mID = 0;
        bool mIsConnected = false;
        IPAddress? mAddress;
        int mPort;
        public ulong ID => mID;
        public Client(int pollingInterval = 16)
        {
            mPollingThread = new Thread(PollingLoop);
            mPollingThread.IsBackground = true;
            mPollingInterval = pollingInterval;
            mIsPolling = false;
            mPort = -1;
        }
        public bool Connect(IPAddress address, int port)
        {

            try
            {
                TCPClient = new TcpClient();
                TCPClient.Connect(address, port);
                TCPClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return false;
            }

            mIsConnected = true;
            mIsPolling = true;
            mAddress = address;
            mPort = port;
            mPollingThread.Start();
            Log.Message("Connected To Server");

            while (mID == 0)
                Thread.Sleep(mPollingInterval);

            return true;            
        }

        public void SendData(ReadOnlySpan<byte> data)
        {
            if (TCPClient is null)
                return;

            if (!TCPClient.Connected && mIsConnected)
            {
                ServerClosed();
                return;
            }

            if (!SendDataHandler.SendData(TCPClient, data, out var exception))
                OnErrorPosted?.Invoke(exception);

        }
        void PollingLoop()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (mIsPolling)
            {
                stopwatch.Restart();
                ReadIncomingData();
                int waitTime = mPollingInterval - (int)stopwatch.ElapsedMilliseconds;
                if (waitTime > 0)
                    Thread.Sleep(waitTime);
            }
        }
        void ReadIncomingData()
        {
            if (TCPClient is null)
                return;

            if (!TCPClient.Connected && mIsConnected)
            {
                ServerClosed();
                return;
            }

            if (mID == 0)
            {
                int retries = 0;
                while (TCPClient.Client.Available < sizeof(ulong))
                {
                    Thread.Sleep(mPollingInterval);
                    ++retries;
                    if (retries >= 5)
                    {
                        Log.Error("Could Not Establish Server Authorized Identifier");
                        OnErrorPosted?.Invoke(new Exception("Could Not Establish Server Authorized Identifier"));
                        return;
                    }
                }

                byte[] idData = new byte[sizeof(ulong)];
                try
                {
                    TCPClient.GetStream().ReadExactly(idData, 0, sizeof(ulong));
                    mID = BitConverter.ToUInt64(idData, 0);
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                    OnErrorPosted?.Invoke(e);
                    mID = 0;
                    return;
                }
                
                Log.Message("ClientID: " + mID);
                return;
            }

            while (true)
            {
                var code = IncomingDataHandler.HandleFromClient(TCPClient, out var data, out var exception);
                if (code == ReadCode.AllDataRead)
                    break;
                else if (code == ReadCode.ReadSuccesful && data is not null)
                    OnDataRecieved?.Invoke(data);
                else if (code == ReadCode.Error)
                    OnErrorPosted?.Invoke(exception ?? new Exception("Reading Error"));
                else if (code == ReadCode.ServerDisconnected)
                {
                    ServerClosed();
                    return;
                }                   
            }  
        }

        void ServerClosed()
        {
            mIsConnected = false;
            Dispose();
            Log.Message("Server Was Shudown");
            OnServerClosed?.Invoke();
        }
        public bool Reconnect()
        {
            if (mIsConnected)
                return false;

            if (mIsPolling)
                return false;

            if (TCPClient is not null)
                return false;

            if (mAddress is null)
                return false;

            if (mPort == -1)
                return false;

            return Connect(mAddress, mPort);
        }
        public void Disconnect() => Dispose();
        ~Client() => Dispose();
        public void Dispose()
        {
            if (mIsDisposed)
                return;

            mIsDisposed = true;
            mIsPolling = false;
            mPollingThread.Join();

            if (TCPClient is not null)
                TCPClient.Dispose();

            TCPClient = null;
        }
    }
}
