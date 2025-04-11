Setting up a simplte single threaded client
`using System.Net;
using System.Text;
using OverTCP.Messaging;
using OverTCP;
using OverTCP.Dispatcher;
using System;

namespace TestClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Enter The IP Address");
            string? ipAdress = Console.ReadLine();

            if (string.IsNullOrEmpty(ipAdress))
                return;

            SingleThreadClient<Messages> client = new SingleThreadClient<Messages>();
            if (!IPAddress.TryParse(ipAdress, out var address) || !client.Connect(address, 25566))
            {
                client.Dispose();
                return;
            }

            while (true)
            {
                if (client.AnyExceptions)
                {
                    foreach (var exception in client.GetExceptions())
                    {
                        Log.Error(exception);
                    }
                }

                var requests = client.GetDataRequests();
                bool quit = false;
                foreach (var request in requests)
                {
                    var message = Encoding.UTF8.GetString(request.mBytes);
                    if (message == "Quit")
                    {
                        quit = true;
                        break;
                    }
                    Console.WriteLine(message);
                }

                if (quit)
                    break;

                Thread.Sleep(16);
            }

            client.Disconnect();
        }
    }
}
`

Setup simple single threaded server
`using OverTCP.Messaging;
using OverTCP;
using System.Text;
using OverTCP.Dispatcher;

namespace TestServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            SingleThreadServer<Messages> server = new();
            server.Start(25566);

            while (!server.HasConnectedClients)
                Thread.Sleep(100);

            for (int i = 0; i < 100_000; i++)
            {
                server.Server.SendToAll(Create.Data(Messages.Placeholder, Header.ALL_ULONG_ID, Random.Shared.Next().ToString()));
                if (server.HasRequests)
                {
                    foreach (var request in server.GetRequests())
                        Server_OnDataRecieved(request);
                }
                Thread.Sleep(16);
            }

            server.Server.SendToAll(Create.Data(Messages.Placeholder, Header.ALL_ULONG_ID, "Quit"));
            Thread.Sleep(1000);
            server.Stop();
        }

        private static void Server_OnDataRecieved(SingleThreadServer<Messages>.DataRequest request)
        {
            Console.WriteLine(request.mType + "From: " + request.mSentByID + " - " + Encoding.UTF8.GetString(request.mBytes));
        }
    }
}
`
