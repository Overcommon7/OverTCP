using System.Net;
using System.Text;
using OverTCP.Messaging;
using OverTCP;
using OverTCP.Dispatcher;
using System;
using OverTCP.File;

namespace TestClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Enter The IP Address");
            string? ipAdress = "192.168.1.79";

            if (string.IsNullOrEmpty(ipAdress))
                return;

            SingleThreadClient<Messages> client = new SingleThreadClient<Messages>();
            if (!IPAddress.TryParse(ipAdress, out var address) || !client.Connect(address, 25566))
            {
                client.Disconnect();
                return;
            }

            var directory = Directory.GetCurrentDirectory() + '\\';
            int count = 0;
            long size = 0;
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

                foreach (var request in requests)
                {
                    if (request.mType == Messages.DirectoryData)
                    {
                        Managment.CreateDirectoriesFromData(directory, request.Span, client.Client);
                    }

                    if (request.mType == Messages.FileData)
                    {
                        ++count;
                        size += request.mBytes.Length;

                        var values = Managment.OnFileDataReceived(request.Span, directory, client.Client);
                        if (count > 100)
                        {
                            Console.WriteLine("Size: " + size);
                            count = 0;
                        }                            

                        if (values.mState == Managment.RecievingState.Complete)
                            Console.WriteLine(values);
                    }
                }                
            }
        }
    }
}
