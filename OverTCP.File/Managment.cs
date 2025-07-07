using OverTCP;
using OverTCP.Messaging;
using System.Buffers;
using System.IO;

namespace OverTCP.File
{
    public static class Managment
    {
        public delegate void FileChunkSent(ReadOnlySpan<byte> fileData, Partial fileState, long currentFileSize);
        static List<(ulong mHash, FileStream mStream)> sFileStreams = [];
        static List<(ulong mHash, AsyncPauseToken mToken)> sPauseTokens = [];
        const int HEADER_SIZE = sizeof(byte) + sizeof(ulong);
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

        static FileStream GetStream(string? directory, FixedString_128 relativePath)
        {
            ulong fileHash = ClientExtenstions.HashString(relativePath.Value);
            var stream = GetStream(fileHash);
            if (stream is null)
            {
                stream = new FileStream(directory + relativePath.Value, FileMode.Create, FileAccess.Write);
                lock (sFileStreams)
                    sFileStreams.Add((fileHash, stream));
            }
            return stream;
        }

        static FileStream? GetStream(ulong fileHash)
        {
            lock (sFileStreams)
            {
                var index = sFileStreams.FindIndex(value => value.mHash == fileHash);
                if (index == -1)
                    return null;

                return sFileStreams[index].mStream;
            }            
        }

        static void CloseStream(FixedString_128 relativePath)
        {
            ulong fileHash = ClientExtenstions.HashString(relativePath.Value);
            CloseStream(fileHash);
        }

        static void CloseStream(ulong fileHash)
        {
            lock (sFileStreams)
            {
                var index = sFileStreams.FindIndex(value => value.mHash == fileHash);
                if (index == -1)
                    return;

                sFileStreams[index].mStream.Close(); 
                sFileStreams.RemoveAt(index);
            }               
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

        public static void CreateDirectoriesFromData(string directory, ReadOnlySpan<byte> directoryData, Action<string>? OnDirectoriesCreated = null)
        {
            directory = Path.GetFullPath(directory);
            if (directory.EndsWith('/'))
                directory = directory.Remove(directory.Length - 1);
            if (!directory.EndsWith('\\'))
                directory += '\\';

            var directories = Format.DataToStructArray<FixedString_128>(directoryData);
            if (directories.Length == 0)
                return;

            string root = directory + new DirectoryInfo(directories[0].Value).Name + '\\';

            if (Directory.Exists(root))
                Directory.Delete(root, true);

            Directory.CreateDirectory(root);

            for (int i = 1; i < directories.Length; i++)
            {
                Directory.CreateDirectory(root + directories[i]);
            }

            OnDirectoriesCreated?.Invoke(root);
        }

        public static async void SendAllFiles(string directory, FileChunkSent SendFileChunk, Action<string>? OnDirectoryComplete = null, Action<long>? OnDirectorySize = null, Action? OnFileError = null)
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
            {
                bool result = await SendSingleFile_Internal(entry, SendFileChunk, directory, OnFileError);
                if (!result) return;
            }

            OnDirectoryComplete?.Invoke(new DirectoryInfo(directory).Name);
        }

        public static async void SendSingleFile(string filepath, FileChunkSent SendFileChunk, string? root = null, Action? OnError = null)
        {
            _ = await SendSingleFile_Internal(filepath, SendFileChunk, root, OnError);
        }

        static async Task<bool> SendSingleFile_Internal(string filepath, FileChunkSent SendFileChunk, string? root = null, Action? OnError = null)
        {
            FixedString_128 filename = new();
            if (string.IsNullOrEmpty(root))
                filename = Path.GetFileName(filepath);
            else
                filename = Path.GetRelativePath(root, filepath);

            ulong fileHash = ClientExtenstions.HashString(filename.Value);
            var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024 * 4); // 4MB;

            FileStream? fileStream = null;
            try
            {
                fileStream = System.IO.File.OpenRead(filepath);
                long bytesRead = 0;

                AsyncPauseToken pauseToken = new();
                lock (sPauseTokens)
                    sPauseTokens.Add((fileHash, pauseToken));
                   
                while (true)
                {
                    var fileInfo = GetFileData(filename, fileStream, ref buffer, fileHash);
                    if (fileInfo.fileState == Partial.ERROR)
                        return false;

                    bytesRead += fileInfo.bytesRead;
                    await pauseToken.WaitIfPausedAsync();

                    if (fileInfo.bufferSize != buffer.Length)
                        SendFileChunk.Invoke(buffer.AsSpan(0, fileInfo.bufferSize), fileInfo.fileState, bytesRead);
                    else
                        SendFileChunk.Invoke(buffer, fileInfo.fileState, bytesRead);

                    if (fileInfo.fileState == Partial.EntireFile || fileInfo.fileState == Partial.EndOfFile)
                        break;
                }
                lock (sPauseTokens)
                {
                    int index = sPauseTokens.FindIndex(lockData => lockData.mHash == fileHash);
                    if (index != -1)
                        sPauseTokens.RemoveAt(index);
                }                

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                OnError?.Invoke();
                return false;
            }
            finally
            {
                lock (sPauseTokens)
                {
                    int index = sPauseTokens.FindIndex(lockData => lockData.mHash == fileHash);
                    if (index != -1)
                        sPauseTokens.RemoveAt(index);
                }

                fileStream?.Close();
                ArrayPool<byte>.Shared.Return(buffer, true);
            }
        }

        public static void PauseFileSending(ulong fileHash)
        {
            lock (sPauseTokens)
            {
                int index = sPauseTokens.FindIndex(tokenData => tokenData.mHash == fileHash);
                if (index == -1)
                    return;

                sPauseTokens[index].mToken.Pause();
            }           
        }

        public static void ResumeFileSending(ulong fileHash)
        {
            lock (sPauseTokens)
            {
                int index = sPauseTokens.FindIndex(tokenData => tokenData.mHash == fileHash);
                if (index == -1)
                    return;

                sPauseTokens[index].mToken.Resume();
            }
        }

        public static (RecievingState mState, ulong mFileHash) OnFileDataReceived(ReadOnlySpan<byte> fileData, string directory)
        {
            if (fileData.IsEmpty)
                return (RecievingState.Error, ulong.MaxValue);

            var partial = fileData[0];
            ReadOnlySpan<byte> buffer;
            FileStream? stream = null;
            try
            {
                if (partial == (byte)Partial.EntireFile || partial == (byte)Partial.StartOfFile)
                {
                    if (!Format.ToStruct(fileData.Slice(1, FixedString_128.Size), out FixedString_128 relativePath))
                    {
                        Log.Error("Could Not Parse Filename");
                        return (RecievingState.Error, ulong.MaxValue);
                    }

                    buffer = fileData.Slice(FixedString_128.Size + 1);
                    stream = GetStream(directory, relativePath);
                }
                else
                {
                    if (!Format.ToStruct(fileData.Slice(sizeof(byte), sizeof(ulong)), out ulong hash))
                        return (RecievingState.Error, ulong.MaxValue);

                    stream = GetStream(hash);
                    buffer = fileData.Slice(HEADER_SIZE);
                }

                if (stream is null)
                {
                    Log.Error("Passed A Null File Stream When File Packet Sent");
                    return (RecievingState.Error, ulong.MaxValue);
                }

                stream.Write(buffer);

                if (partial == (byte)Partial.EntireFile)
                {
                    _ = Format.ToStruct(fileData.Slice(1, FixedString_128.Size), out FixedString_128 relativePath);
                    ulong hash = ClientExtenstions.HashString(relativePath.Value);
                    CloseStream(hash);
                    return (RecievingState.Complete, hash);
                }

                if (partial == (byte)Partial.EndOfFile)
                {
                    _ = Format.ToStruct(fileData.Slice(sizeof(byte), sizeof(ulong)), out ulong hash);
                    CloseStream(hash);
                    return (RecievingState.Complete, hash);
                }

                if (!Format.ToStruct(fileData.Slice(sizeof(byte), sizeof(ulong)), out ulong fileHash))
                    return (RecievingState.Error, ulong.MaxValue);

                return (RecievingState.InProgress, fileHash);     
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return (RecievingState.Error, ulong.MaxValue);
            }
        }

        public static (Partial fileState, int bytesRead, int bufferSize) GetFileData(FixedString_128 filename, FileStream stream, ref byte[] buffer, ulong fileHash)
        {
            buffer[0] = (byte)Partial.FileSegment;
            int index = 1;
            try
            {
                if (stream.Position == 0)
                {
                    buffer[0] = (byte)Partial.StartOfFile;
                    filename.Bytes.CopyTo(buffer.AsSpan().Slice(1, FixedString_128.Size));
                    index += FixedString_128.Size;
                }
                else
                {
                    fileHash.Pack().CopyTo(buffer.AsSpan(1, sizeof(ulong)));
                    index += HEADER_SIZE;
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

                return ((Partial)buffer[0], bytesRead, bytesRead + index);
            }
            catch (Exception ex)
            {

                Log.Error(ex.Message);
                return (Partial.ERROR, 0, 0);
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

        public static void SendFileData(Client client, ReadOnlySpan<byte> fileChunk)
        {
            client.SendData(Create.Data(Messages.FileData, client.ID, fileChunk));
        }

        public static void SendFileChunkTo(Server server, ReadOnlySpan<byte> fileChunk, ulong clientID)
        {
            server.SendTo(clientID, Create.Data(Messages.FileData, clientID, fileChunk));
        }

        public static void SendFileChunk(Server server, ReadOnlySpan<byte> fileChunk)
        {
            server.SendToAll(Create.Data(Messages.FileData, Header.ALL_ULONG_ID, fileChunk));
        }

        public static void OnDirectoriesCreated(Server server, ulong clientID)
        {
            server.SendTo(clientID, Header.CreateHeader(Messages.ReadyForFiles, clientID));
        }

        public static void OnDirectoriesCreated(Client client)
        {
            client.SendData(Header.CreateHeader(Messages.ReadyForFiles, client.ID));
        }
    }
}
