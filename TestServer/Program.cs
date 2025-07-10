using OverTCP.Messaging;
using OverTCP;
using System.Text;
using OverTCP.Dispatcher;
using OverTCP.File;

namespace TestServer
{
    internal class Program
    {
        static string mDirectoryPath = string.Empty;
        static Server server;
        static Program()
        {
            server = new();
        }
        static void Main(string[] args)
        {
            server.Start(25566);
            server.OnDataRecieved += Server_OnDataRecieved;

            while (server.ClientCount == 0)
                Thread.Sleep(100);

            static void SendFileChunk(ReadOnlySpan<byte> fileChunk, Managment.FileState partial, long bytesRead)
            {
                Managment.SendFileChunk(server, fileChunk);
            }

            while (true)
            {
                var line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    continue;

                if (line.StartsWith('Q'))
                    break;

                if (File.Exists(line))
                {
                    Managment.SendSingleFile(line, SendFileChunk, server);
                }

                if (Directory.Exists(line))
                {
                    var data = Managment.GetDirectoryData(line);
                    mDirectoryPath = line;
                }
            }
            server.Stop();
        }

        private static void Server_OnDataRecieved(ulong clientID, Memory<byte> data)
        {
            Extract.Header(data.Span, out Messages message, out ulong _);
            if (message == Messages.ReadyForFiles)
            {
                Managment.SendAllFiles(mDirectoryPath, (fileData, fileState, currentFileSize) =>
                {
                    Managment.SendFileChunkTo(server, fileData, clientID);
                }, server);
            }
        }
    }
}
