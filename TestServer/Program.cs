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
            Log.OnMessagePosted += Log_OnMessagePosted;

            while (server.ClientCount == 0)
                Thread.Sleep(100);

            while (true)
            {
                var line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    continue;

                if (line.StartsWith('Q'))
                    break;

                if (File.Exists(line))
                {
                    Managment.SendSingleFile(line, server);
                }

                if (Directory.Exists(line))
                {
                    Managment.SendAllFiles(line, server);
                    mDirectoryPath = line;
                }
            }
            server.Stop();
        }

        private static void Log_OnMessagePosted(string arg1, string arg2, Log.Severity arg3)
        {
            Console.WriteLine(arg1);
            if (!string.IsNullOrEmpty(arg2))
                Console.WriteLine(arg2);
        }
    }
}
