using OverTCP;
using OverTCP.Messaging;
using System.IO;
using Windows.Media.Protection.PlayReady;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OverTCP.File
{
    public static class Managment
    {
        public delegate void FileChunkSent(byte[] fileData, Partial fileState, long currentFileSize);

        public enum Partial : byte
        {
            ERROR = 255,

            StartOfFile = 1,
            FileSegment = 2,
            EndOfFile = 3,
            EntireFile = 4
        }

        public enum RecievingState : byte
        {
            Error = 255,

            InProgress = 0,
            Complete = 1,            
        }

        public static ReadOnlySpan<byte> GetDirectoryData(string directory)
        {
            if (!Directory.Exists(directory))
                return [];

            var directories = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories);
            FixedString_128[] mDirectoryData = new FixedString_128[directories.Length + 1];
            mDirectoryData[0] = Path.GetFullPath(directory);
            int index = 1;
            foreach (var dir in directories)
            {
                mDirectoryData[index] = Path.GetRelativePath(directory, dir);
                ++index;
            }

            return Format.StructArrayToData(mDirectoryData);
        }

        public static void CreateDirectoriesFromData(string directory, ReadOnlySpan<byte> directoryData, Action<string> OnRootDirectoryCreated)
        {
            directory = Path.GetFullPath(directory);
            if (directory.EndsWith('/'))
                directory = directory.Remove(directory.Length - 1);
            if (!directory.EndsWith('\\'))
                directory += '\\';

            var directories = Format.DataToStructArray<FixedString_128>(directoryData);
            if (directories.Length == 0)
                return;

            string root = directory + Path.GetFileName(directories[0]) + '\\';

            if (Directory.Exists(root))
                Directory.Delete(root, true);

            Directory.CreateDirectory(root);
            OnRootDirectoryCreated(root);
 
            for (int i = 1; i < directories.Length; i++)
            {
                Directory.CreateDirectory(root + directories[i]);
            }
        }

        public static void SendAllFiles(string directory, FileChunkSent SendFileChunk, Action<long>? OnDirectorySize)
        {
            if (!Directory.Exists(directory))
                return;

            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);

            if (OnDirectorySize is not null)
            {
                long totalSize = 0;
                foreach (var file in files)             
                    totalSize += new FileInfo(file).Length;

                OnDirectorySize.Invoke(totalSize);
            }


            foreach (var entry in files)
                SendSingleFile(entry, SendFileChunk, directory);
        }

        public static bool SendSingleFile(string filepath, FileChunkSent SendFileChunk, string? root = null)
        {
            try
            {
                var fileStream = System.IO.File.OpenRead(filepath);
                var buffer = new byte[int.MaxValue / 2];
                long bytesRead = 0;
                while (true)
                {
                    var data = GetFileData(filepath, fileStream, ref buffer, root);
                    if (data.fileState == Partial.ERROR)
                        return false;

                    bytesRead += data.bytesRead;
                    SendFileChunk.Invoke(buffer, data.fileState, bytesRead);

                    if (data.fileState == Partial.EntireFile || data.fileState == Partial.EndOfFile)
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return false;
            }
        }

        public static RecievingState OnFileDataReceived(ReadOnlySpan<byte> fileData, string directory, ref FileStream? stream)
        {
            if (fileData.IsEmpty)
                return RecievingState.Error;

            var partial = fileData[0];
            ReadOnlySpan<byte> buffer;
            try
            {
                if (partial == (byte)Partial.EntireFile || partial == (byte)Partial.StartOfFile)
                {
                    if (!Format.ToStruct(fileData.Slice(1, FixedString_128.Size), out FixedString_128 relativePath))
                    {
                        Log.Error("Could Not Parse Filename");
                        return RecievingState.Error;
                    }

                    stream = new FileStream(directory + relativePath.Value, FileMode.Create, FileAccess.Write);
                    buffer = fileData.Slice(FixedString_128.Size + 1);
                }
                else
                {
                    buffer = fileData.Slice(1);
                }

                if (stream is null)
                {
                    Log.Error("Passed A Null File Stream When File Packet Sent");
                    return RecievingState.Error;
                }

                stream.Write(buffer);

                if (partial == (byte)Partial.EntireFile || partial == (byte)Partial.EndOfFile)
                {
                    stream.Close();
                    stream = null;
                    return RecievingState.Complete;
                }

                return RecievingState.InProgress;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return RecievingState.Error;
            }                
        }

        public static (Partial fileState, int bytesRead) GetFileData(string filepath, FileStream stream, ref byte[] buffer, string? root)
        {
            buffer[0] = (byte)Partial.FileSegment;
            int index = 1;
            try
            {
                if (stream.Position == 0)
                {
                    FixedString_128 filename = new();
                    if (string.IsNullOrEmpty(root))
                        filename = Path.GetFileName(filepath);
                    else
                        filename = Path.GetRelativePath(root, filepath);

                    buffer[0] = (byte)Partial.StartOfFile;
                    filename.Bytes.CopyTo(buffer.AsSpan().Slice(1, FixedString_128.Size));
                    index += FixedString_128.Size;
                }

                int count = buffer.Length - index;
                int bytesRead = stream.Read(buffer, index, count);
                if (stream.Position == stream.Length)
                {
                    if (buffer[0] == (byte)Partial.StartOfFile)
                        buffer[0] = (byte)Partial.EntireFile;
                    else
                        buffer[0] = (byte)Partial.EndOfFile;
                }

                if (bytesRead < count)
                    Array.Resize(ref buffer, bytesRead + index);

                return ((Partial)buffer[0], bytesRead);
            }
            catch (Exception ex)
            {

                Log.Error(ex.Message);
                return (Partial.ERROR, 0);
            }
        }

        static FileStream? FromData(string directory, ReadOnlySpan<byte> data)
        {
            if (!Format.ToStruct(data.Slice(0, FixedString_64.Size), out FixedString_64 filename))
                return null;

            var fileData = data.Slice(FixedString_64.Size).ToArray();

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var path = directory + '\\' + filename;
            var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
            stream.Write(fileData);
            return stream;
        }

        public static void SendFileData(Client client, byte[] fileChunk)
        {
            client.SendData(Create.Data(Messages.FileData, client.ID, fileChunk));
        }

        public static void SendFileChunkTo(Server server, byte[] fileChunk, ulong clientID)
        {
            server.SendTo(clientID, Create.Data(Messages.FileData, clientID, fileChunk));
        }

        public static void SendFileChunk(Server server, byte[] fileChunk)
        {
            server.SendToAll(Create.Data(Messages.FileData, Header.ALL_ULONG_ID, fileChunk));
        }
    }
}
