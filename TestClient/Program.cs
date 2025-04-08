using System.Net;
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
                client.Disconnect();
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
                client.SendData(Messages.Placeholder, Random.Shared.Next().ToString());
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

                Thread.Sleep(Random.Shared.Next(7, 33));
            }

            client.Disconnect();
        }
    }
}
