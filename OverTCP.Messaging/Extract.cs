using System.Text;

namespace OverTCP.Messaging
{
    public static class Extract
    {
        public static Span<byte> Contents(Span<byte> data, out int message, out int clientID, out int gameID)
        {
            message = BitConverter.ToInt32(data.Slice(0, sizeof(int)));
            clientID = BitConverter.ToInt32(data.Slice(sizeof(int) * 2, sizeof(int)));
            gameID = BitConverter.ToInt32(data.Slice(sizeof(int) * 3, sizeof(int)));
            return MessageData(data);
        }

        public static Span<byte> All<T>(Span<byte> data, out T message, out int id)
            where T : struct, Enum
        {
            Header(data, out message, out id);
            return MessageData(data);
        }

        public static Span<byte> All<T>(Span<byte> data, out T message, out ulong id)
            where T : struct, Enum
        {
            Header(data, out message, out id);
            return MessageData(data);
        }

        public static Memory<byte> AllAsMemory<T>(Memory<byte> data, out T message, out int id)
            where T : struct, Enum
        {
            Header(data.Span, out message, out id);
            return MessageDataAsMemory(data);
        }

        public static Memory<byte> AllAsMemory<T>(Memory<byte> data, out T message, out ulong id)
            where T : struct, Enum
        {
            Header(data.Span, out message, out id);
            return MessageDataAsMemory(data);
        }

        public static Span<byte> Any(Span<byte> data, out int message, out ulong id)
        {
            message = BitConverter.ToInt32(data.Slice(0, sizeof(int)));
            id = BitConverter.ToUInt64(data.Slice(sizeof(int) * 2, sizeof(ulong)));
            return MessageData(data);
        }
        public static void Header<T>(ReadOnlySpan<byte> buffer, out T message, out int id)
            where T : struct, Enum
        {
            int messageID = BitConverter.ToInt32(buffer.Slice(0, sizeof(int)));
            id = BitConverter.ToInt32(buffer.Slice(sizeof(int) * 2, sizeof(int)));
            message = (T)((object)messageID);
        }

        public static void Header<T>(ReadOnlySpan<byte> buffer, out T message, out ulong id)
            where T : struct, Enum
        {
            int messageID = BitConverter.ToInt32(buffer.Slice(0, sizeof(int)));
            id = BitConverter.ToUInt64(buffer.Slice(sizeof(int) * 2, sizeof(ulong)));
            message = (T)((object)messageID);
        }

        public static Span<byte> MessageData(Span<byte> data)
        {
            var style = (Header.Style)BitConverter.ToInt32(data.Slice(4, sizeof(int)));
            switch (style)
            {
                case Messaging.Header.Style.IntegerID:
                    if (data.Length <= sizeof(int) * 3)
                        return Span<byte>.Empty;
                    return data.Slice(sizeof(int) * 3);
                case Messaging.Header.Style.ULongID:
                    if (data.Length <= (sizeof(int) * 2) + sizeof(ulong))
                        return Span<byte>.Empty;
                    return data.Slice((sizeof(int) * 2) + sizeof(ulong));
                case Messaging.Header.Style.StringID:
                case Messaging.Header.Style.IArguments:
                case Messaging.Header.Style.Miscelaneous:
                    int length = BitConverter.ToInt32(data.Slice(sizeof(int) * 2, sizeof(int)));
                    return data.Slice(length + sizeof(int));
            }
            return data;
        }

        public static Memory<byte> MessageDataAsMemory(Memory<byte> data)
        {

            var style = (Header.Style)BitConverter.ToInt32(data.Slice(4, sizeof(int)).Span);
            switch (style)
            {
                case Messaging.Header.Style.IntegerID:
                    if (data.Length <= sizeof(int) * 3)
                        return Memory<byte>.Empty;
                    return data.Slice(sizeof(int) * 3);
                case Messaging.Header.Style.ULongID:
                    if (data.Length <= (sizeof(int) * 2) + sizeof(ulong))
                        return Memory<byte>.Empty;
                    return data.Slice((sizeof(int) * 2) + sizeof(ulong));
                case Messaging.Header.Style.StringID:
                case Messaging.Header.Style.IArguments:
                case Messaging.Header.Style.Miscelaneous:
                    int length = BitConverter.ToInt32(data.Slice(sizeof(int) * 2, sizeof(int)).Span);
                    return data.Slice(length + sizeof(int));
            }
            return data;
        }

        public static string MessageDataAsString(Span<byte> data)
        {
            return Encoding.UTF8.GetString(MessageData(data));
        }
    }
}
