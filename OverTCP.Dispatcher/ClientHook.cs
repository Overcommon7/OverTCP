using OverTCP.Messaging;
using System.Collections.Concurrent;

namespace OverTCP.Dispatcher
{
    internal class ClientHook<T> : IDisposable where T : struct, Enum
    {
        bool mDisposed = false;
        bool mReconnected = false;
        Client mClient;

        internal bool DidReconnect => mReconnected;
        internal bool HasExceptions => mExceptions.Count > 0;
        
        ConcurrentBag<SingleThreadClient<T>.DataRequest> mRequests;
        List<Exception> mExceptions;
        internal ClientHook(Client client)
        {
            client.OnDataRecieved += Client_OnDataRecieved;
            client.OnErrorPosted += Client_OnErrorPosted;
            client.OnReconnected += Client_OnReconnected;

            mClient = client;
            mRequests = new();
            mExceptions = new();
        }

        private void Client_OnReconnected()
        {
            mReconnected = true;
        }

        private void Client_OnErrorPosted(Exception exception)
        {
            lock (mExceptions)
                mExceptions.Add(exception);
        }

        private void Client_OnDataRecieved(byte[] data)
        {
            SingleThreadClient<T>.DataRequest request = new();
            request.mBytes = Extract.All(data, out request.mType, out request.mHeaderID).ToArray();
            mRequests.Add(request);
        }

        internal SingleThreadClient<T>.DataRequest[] GetRequests()
        {
            mReconnected = false;
            if (mRequests.Count == 0)
                return [];

            SingleThreadClient<T>.DataRequest[] requests;
            requests = mRequests.ToArray();
            mRequests.Clear();
            return requests;
        }

        internal Exception[] GetExceptions()
        {
            if (mExceptions.Count == 0)
                return [];

            Exception[] exceptions;
            lock (mExceptions)
            {
                exceptions = mExceptions.ToArray();
                mExceptions.Clear();
            }

            return exceptions;
        }

        public void Dispose()
        {
            if (mDisposed) 
                return;   

            mDisposed = true;
            mClient.OnDataRecieved += Client_OnDataRecieved;
            mClient.OnErrorPosted += Client_OnErrorPosted;
            mClient.OnReconnected += Client_OnReconnected;
        }

        ~ClientHook()
        {
            Dispose();
        }
    }
}
