using OverTCP;
using OverTCP.Messaging;
using System;
using System.Buffers;
using System.IO;
using System.Reflection;
using static OverTCP.File.Managment;

namespace OverTCP.File
{
    public static class Managment
    {
        class InternalValues()
        {
            public FileStream? mStream = null;
            public Task mTask = Task.CompletedTask;
            public long mSize = 0;
            public int mIsPaused = 0;
        }

        public readonly struct FileChunkData(FileState state, int chunkSize, long currentSize, long totalSize)
        {
            public readonly FileState mState = state;
            public readonly int mChunkSize = chunkSize;
            public readonly long mCurrentSizeOfFile = currentSize;
            public readonly long mTotalFileSize = totalSize;
        }

        static List<(ulong mHash, InternalValues mValues)> sFileStreams = [];
        static List<(ulong mHash, AsyncPauseToken mToken)> sPauseTokens = [];
        const int HEADER_SIZE = sizeof(byte) + sizeof(ulong);
        public enum FileState : byte
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

        static InternalValues GetStream(string? directory, FixedString_128 relativePath)
        {
            ulong fileHash = ClientExtenstions.HashString(relativePath.Value);
            var values = GetInternalValues(fileHash);
            if (values.mStream is null)
            {
                values.mStream = new FileStream(directory + relativePath.Value, FileMode.Create, FileAccess.Write, FileShare.None);
                lock (sFileStreams)
                    sFileStreams.Add((fileHash, values));
            }

            return values;
        }

#nullable disable
        static InternalValues GetInternalValues(ulong fileHash)
        {
            lock (sFileStreams)
            {
                var index = sFileStreams.FindIndex(value => value.mHash == fileHash);
                if (index == -1)
                    return new();

                return sFileStreams[index].Item2;
            }            
        }
#nullable enable

        static void CloseStream(FixedString_128 relativePath)
        {
            ulong fileHash = ClientExtenstions.HashString(relativePath.Value);
            CloseStream(fileHash);
        }

        static async void CloseStream(ulong fileHash)
        {

            InternalValues? values;
            lock (sFileStreams)
            {
                int index = -1;
                index = sFileStreams.FindIndex(value => value.mHash == fileHash);
                if (index == -1)
                    return;

                values = sFileStreams[index].mValues;
                sFileStreams.RemoveAt(index);
            }

            if (values is null)
                return;

            await values.mTask;
            values.mStream?.Close();
        }

        public static ReadOnlySpan<byte> GetDirectoryData(string directory)
        {
            if (!Directory.Exists(directory))
                return ReadOnlySpan<byte>.Empty;

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

        public static void CreateDirectoriesFromData<T>(string directory, Span<byte> directoryData, ulong directoryHash, T clientOrServer, ulong? sendingTo = null, Action<string>? OnDirectoriesCreated = null)
            where T : class
        {
            directory = Path.GetFullPath(directory);
            if (directory.EndsWith('/'))
                directory = directory.Remove(directory.Length - 1);
            if (!directory.EndsWith('\\'))
                directory += '\\';

            var directories = Format.DataToStructArray<FixedString_128>(directoryData);
            if (directories.Length == 0)
            {
                Log.Error("Directory Data Was Empty");
                return;
            }
                
            string root = directory + new DirectoryInfo(directories[0].Value).Name + '\\';

            if (Directory.Exists(root))
                Directory.Delete(root, true);

            Directory.CreateDirectory(root);

            for (int i = 1; i < directories.Length; i++)
            {
                Directory.CreateDirectory(root + directories[i]);
            }

            OnDirectoriesCreated?.Invoke(root);
            Managment.OnDirectoriesCreated(directoryHash, clientOrServer, sendingTo);            
        }

        public static async void SendAllFiles<T>(string directory, T serverOrClient, ulong? sendingTo = null, Action<FileChunkData>? OnChunkSent = null,
            Action<T, string, ulong>? OnDirectoryComplete = null, Action<T, long, ulong>? OnDirectorySize = null, Action? OnFileError = null)
            where T : class
        {
            if (!Directory.Exists(directory))
                return;

            var directoryName = new DirectoryInfo(directory).FullName;
            ulong directoryHash = ClientExtenstions.HashString(directoryName);
            AsyncPauseToken pauseToken = new();

            void OnClientData(Memory<byte> data)
            {
                var messageData = Extract.AllAsMemory(data, out Messages message, out ulong hash);
                if (hash != directoryHash)
                    return;

                if (message == Messages.ReadyForFiles)
                    pauseToken.Resume();
            }

            void OnServerData(ulong clientID, Memory<byte> data)
            {
                OnClientData(data);
            }

            var directoryData = GetDirectoryData(directory).ToArray();

            {
                if (serverOrClient is Server server)
                    server.OnDataRecieved += OnServerData;
                else if (serverOrClient is Client client)
                    client.OnDataRecieved += OnClientData;
            }
           
            pauseToken.Pause();
            SendDirectoryData(directoryData, directoryHash, serverOrClient, sendingTo);
            await pauseToken.WaitIfPausedAsync();

            {
                if (serverOrClient is Server server)
                    server.OnDataRecieved -= OnServerData;
                else if (serverOrClient is Client client)
                    client.OnDataRecieved -= OnClientData;
            }
            

            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);

            if (OnDirectorySize is not null)
            {
                long totalSize = 0;
                foreach (var file in files)
                    totalSize += new FileInfo(file).Length;

                OnDirectorySize.Invoke(serverOrClient, totalSize, directoryHash);
            }

            foreach (var entry in files)
            {
                bool result = await SendSingleFile_Internal(entry, serverOrClient, sendingTo, OnChunkSent, directory, OnFileError);
                if (!result) return;
            }
            
            OnDirectoryComplete?.Invoke(serverOrClient, directoryName, directoryHash);
        }

        public static void SendSingleFile<T>(string filepath, T serverOrClient, ulong? sendingTo = null, string? root = null, Action<FileChunkData>? OnChunkSent = null, Action? OnError = null) where T : class
        {
            if (serverOrClient is not Client && serverOrClient is not Server)
            {
                Log.Error("Passed In Non-Server/Client Object Into Send File Function: " + serverOrClient?.GetType().Name);
                return;
            }
            _ = SendSingleFile_Internal(filepath, serverOrClient, sendingTo, OnChunkSent, root, OnError);
        }

        static async Task<bool> SendSingleFile_Internal<T>(string filepath, T serverOrClient, ulong? sendingTo, Action<FileChunkData>? OnChunkSent, 
            string? root = null, Action? OnError = null) where T : class
        {
            FixedString_128 filename = new();
            if (string.IsNullOrEmpty(root))
                filename = Path.GetFileName(filepath);
            else
                filename = Path.GetRelativePath(root, filepath);

            ulong fileHash = ClientExtenstions.HashString(filename.Value);
            var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024 * 4); // 4MB;

            FileStream? fileStream = null;

            void OnClientData(Memory<byte> data)
            {
                Extract.Header(data.Span, out Messages message, out ulong hash);

                if (hash != fileHash)
                    return;

                if (message == Messages.PauseFileChunks)
                    PauseFileSending(fileHash);
                if (message == Messages.ResumeFileChunks)
                    ResumeFileSending(fileHash);
            }

            void OnServerData(ulong id, Memory<byte> data)
            {
                OnClientData(data);
            }

            {
                if (serverOrClient is Client client)
                    client.OnDataRecieved += OnClientData;
                if (serverOrClient is Server server)
                    server.OnDataRecieved += OnServerData;
            }           

            try
            {
                fileStream = System.IO.File.OpenRead(filepath);
                long bytesRead = 0;

                AsyncPauseToken pauseToken = new();
                lock (sPauseTokens)
                    sPauseTokens.Add((fileHash, pauseToken));

                long totalLength = fileStream.Length;
                while (true)
                {
                    var fileInfo = GetFileData(filename, fileStream, ref buffer, fileHash);
                    if (fileInfo.fileState == FileState.ERROR)
                        return false;

                    bytesRead += fileInfo.bytesRead;
                    await pauseToken.WaitIfPausedAsync();

                    if (fileInfo.bufferSize != buffer.Length)
                        SendFileChunk(serverOrClient, buffer.AsSpan(0, fileInfo.bufferSize), sendingTo);
                    else
                        SendFileChunk(serverOrClient, buffer, sendingTo);

                    if (OnChunkSent is not null)
                    {
                        FileChunkData data = new(fileInfo.fileState, fileInfo.bytesRead, bytesRead, totalLength);
                        OnChunkSent.Invoke(data);
                    }
                    
                    if (fileInfo.fileState == FileState.EntireFile || fileInfo.fileState == FileState.EndOfFile)
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

                if (serverOrClient is Client client)
                    client.OnDataRecieved -= OnClientData;
                if (serverOrClient is Server server)
                    server.OnDataRecieved -= OnServerData;
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
            
            Log.Message("Paused Sending: " + fileHash);
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

            Log.Message("Resumed Sending: " + fileHash);
        }

        public static (RecievingState mState, ulong mFileHash) OnFileDataReceived<T>(ReadOnlySpan<byte> fileData, string directory, T clientOrServer, ulong? receivingFrom = null) where T : class
        {
            if (clientOrServer is not Client && clientOrServer is not Server)
            {
                Log.Error("Passed In Invalid Client Or Server Object: " + clientOrServer.GetType().Name);
                return (RecievingState.Error, ulong.MaxValue);
            }

            if (fileData.IsEmpty)
                return (RecievingState.Error, ulong.MaxValue);

            var partial = fileData[0];
            ReadOnlySpan<byte> buffer;
            InternalValues values;
            ulong fileHash;
            try
            {
                if (partial == (byte)FileState.EntireFile || partial == (byte)FileState.StartOfFile)
                {
                    if (!Format.ToStruct(fileData.Slice(1, FixedString_128.Size), out FixedString_128 relativePath))
                    {
                        Log.Error("Could Not Parse Filename");
                        return (RecievingState.Error, ulong.MaxValue);
                    }

                    buffer = fileData.Slice(FixedString_128.Size + 1);
                    values = GetStream(directory, relativePath);
                    fileHash = ClientExtenstions.HashString(relativePath.Value);
                    Console.WriteLine(fileHash);
                }
                else
                {
                    if (!Format.ToStruct(fileData.Slice(sizeof(byte), sizeof(ulong)), out fileHash))
                        return (RecievingState.Error, ulong.MaxValue);

                    values = GetInternalValues(fileHash);
                    buffer = fileData.Slice(HEADER_SIZE);
                }

                if (values.mStream is null)
                {
                    Log.Error("Passed A Null File Stream When File Packet Sent");
                    return (RecievingState.Error, ulong.MaxValue);
                }

                int length = buffer.Length;
                Interlocked.Add(ref values.mSize, length);

                void ResumeFileSending()
                {
                    if (clientOrServer is Server server)
                    {
                        if (receivingFrom is not null)
                            server.SendTo(receivingFrom.Value, Header.CreateHeader(Messages.ResumeFileChunks, fileHash));
                        else
                            server.SendToAll(Header.CreateHeader(Messages.ResumeFileChunks, fileHash));
                    }
                    else if (clientOrServer is Client client)
                    {
                        client.SendData(Header.CreateHeader(Messages.ResumeFileChunks, fileHash));
                    }

                    Log.Message("Resumed Receiving: " + fileHash);
                }

                var rentedBuffer = ArrayPool<byte>.Shared.Rent(length);
                buffer.CopyTo(rentedBuffer);

                values.mTask = values.mTask.ContinueWith(_ => {
                    
                    if (values.mSize > int.MaxValue / 2 && values.mIsPaused == 0)
                    {
                        Interlocked.Exchange(ref values.mIsPaused, 1);
                        if (clientOrServer is Server server)
                        {
                            if (receivingFrom is not null)
                                server.SendTo(receivingFrom.Value, Header.CreateHeader(Messages.PauseFileChunks, fileHash));
                            else
                                server.SendToAll(Header.CreateHeader(Messages.PauseFileChunks, fileHash));
                        }
                        else if (clientOrServer is Client client)
                        {
                            client.SendData(Header.CreateHeader(Messages.PauseFileChunks, fileHash));
                        }
                         
                        Log.Message("Paused Receiving: " + fileHash);
                    }

                    return values.mStream.WriteAsync(rentedBuffer, 0, length)
                        .ContinueWith(writeTask => {

                            ArrayPool<byte>.Shared.Return(rentedBuffer);
                            Interlocked.Add(ref values.mSize, -length);

                            if (values.mSize <= (10 * 1024 * 1024) /*10 MB*/ && values.mIsPaused > 0)
                            {
                                Interlocked.Exchange(ref values.mIsPaused, 0);
                                ResumeFileSending();
                            }

                            if (writeTask.IsFaulted)
                                throw writeTask.Exception!;

                        }, TaskScheduler.Default);

                }, TaskScheduler.Default).Unwrap();

                if (partial == (byte)FileState.EntireFile)
                {
                    CloseStream(fileHash);
                    return (RecievingState.Complete, fileHash);
                }

                if (partial == (byte)FileState.StartOfFile)
                {
                    return (RecievingState.InProgress, fileHash);
                }

                if (partial == (byte)FileState.EndOfFile)
                {
                    CloseStream(fileHash);
                    return (RecievingState.Complete, fileHash);
                }

                return (RecievingState.InProgress, fileHash);     
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return (RecievingState.Error, ulong.MaxValue);
            }
        }

        public static (FileState fileState, int bytesRead, int bufferSize) GetFileData(FixedString_128 filename, FileStream stream, ref byte[] buffer, ulong fileHash)
        {
            buffer[0] = (byte)FileState.FileSegment;
            int index = 1;
            try
            {
                if (stream.Position == 0)
                {
                    buffer[0] = (byte)FileState.StartOfFile;
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
                    if (buffer[0] == (byte)FileState.StartOfFile)
                        buffer[0] = (byte)FileState.EntireFile;
                    else
                        buffer[0] = (byte)FileState.EndOfFile;
                }

                return ((FileState)buffer[0], bytesRead, bytesRead + index);
            }
            catch (Exception ex)
            {

                Log.Error(ex.Message);
                return (FileState.ERROR, 0, 0);
            }
        }

        public static void SendFileChunk<T>(T serverOrClient, ReadOnlySpan<byte> fileChunk, ulong? sendingTo)
        {
            if (serverOrClient is Server server)
            {
                if (sendingTo.HasValue)
                    server.SendTo(sendingTo.Value, Create.Data(Messages.FileData, Header.ALL_ULONG_ID, fileChunk));
                else
                    server.SendToAll(Create.Data(Messages.FileData, Header.ALL_ULONG_ID, fileChunk));
            }
            else if (serverOrClient is Client client)
            {
                client.SendData(Create.Data(Messages.FileData, client.ID, fileChunk));
            }
            else
            {
                Log.Error("Server Or Client Object Was Not A Reconzied Type: " + serverOrClient?.GetType().Name);
            }
        }

        public static void OnDirectoriesCreated<T>(ulong directoryHash, T serverOrClient, ulong? sendingTo = null) where T : class
        {
            if (serverOrClient is Server server)
            {
                if (sendingTo.HasValue)
                    server.SendTo(sendingTo.Value, Header.CreateHeader(Messages.ReadyForFiles, directoryHash));
                else
                    server.SendToAll(Header.CreateHeader(Messages.ReadyForFiles, directoryHash));
            }
            else if (serverOrClient is Client client)
            {
                client.SendData(Header.CreateHeader(Messages.ReadyForFiles, directoryHash));
            }
            else
            {
                Log.Error("Server Or Client Object Was Not A Reconzied Type: " + serverOrClient?.GetType().Name);
            }
        }

        public static void SendDirectoryData<T>(ReadOnlySpan<byte> directoryData, ulong directoryHash, T serverOrClient, ulong? sendingTo = null) where T : class
        {
            if (serverOrClient is Server server)
            {
                if (sendingTo.HasValue)
                    server.SendTo(sendingTo.Value, Create.Data(Messages.DirectoryData, directoryHash, directoryData));
                else
                    server.SendToAll(Create.Data(Messages.DirectoryData, directoryHash, directoryData));
            }
            else if (serverOrClient is Client client)
            {
                client.SendData(Create.Data(Messages.DirectoryData, directoryHash, directoryData));
            }
            else
            {
                Log.Error("Server Or Client Object Was Not A Reconzied Type: " + serverOrClient?.GetType().Name);
            }
        }
    }
}
