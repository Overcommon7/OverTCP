using OverTCP.Messaging;
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
                Thread.Sleep(Random.Shared.Next(7, 33));
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
