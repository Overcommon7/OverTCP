using OverTCP.Messaging;
using OverTCP;
using System.Text;
using OverTCP.Dispatcher;
using OverTCP.File;

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

            void SendFileChunk(byte[] fileChunk, Managment.Partial partial, long bytesRead)
            {
                Managment.SendFileChunk(server.Server, fileChunk);
                Console.WriteLine(partial);
            }

            while (true)
            {
                var line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    continue;

                if (line.StartsWith('Q'))
                    break;

                if (!File.Exists(line))
                    continue;


                Managment.SendSingleFile(line, SendFileChunk);
            }
            server.Stop();
        }

        private static void Server_OnDataRecieved(SingleThreadServer<Messages>.DataRequest request)
        {
            Console.WriteLine(request.mType + "From: " + request.mSentByID);
        }
    }
}
