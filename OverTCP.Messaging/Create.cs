using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;

namespace OverTCP.Messaging
{
    public static class Create
    {
        public static byte[] Data<T1, T2>(T1 messageName, IArguments arguments, T2 data)
            where T1 : struct, Enum 
            where T2 : unmanaged
        {
            return Data(messageName, arguments, Format.ToData(data));
        }
        public static byte[] Data<T>(T messageName, IArguments arguments, ReadOnlySpan<byte> data)
            where T : struct, Enum
        {
            return Data(Header.CreateHeader(messageName, arguments), data);
        }

        public static byte[] Data<T>(T messageName, int id, ReadOnlySpan<byte> data)
            where T : struct, Enum
        {
            return Data(Header.CreateHeader(messageName, id), data);
        }

        public static byte[] Data<T>(T messageName, ulong id, ReadOnlySpan<byte> data)
           where T : struct, Enum
        {
            return Data(Header.CreateHeader(messageName, id), data);
        }

        public static byte[] Data<T>(T messageName, ulong id, string data)
           where T : struct, Enum
        {
            return Data(Header.CreateHeader(messageName, id), Encoding.UTF8.GetBytes(data));
        }

        public static byte[] Data<T>(T messageName, int id, string data)
           where T : struct, Enum
        {
            return Data(Header.CreateHeader(messageName, id), Encoding.UTF8.GetBytes(data));
        }

        public static byte[] Data<T1, T2>(T1 messageName, int id, T2 data)
            where T1 : struct, Enum
            where T2 : unmanaged
        {
            return Data(Header.CreateHeader(messageName, id), Format.ToData(data));
        }

        public static byte[] Data<T1, T2>(T1 messageName, ulong id, T2 data)
           where T1 : struct, Enum
           where T2 : unmanaged
        {
            return Data(Header.CreateHeader(messageName, id), Format.ToData(data));
        }

        public static byte[] Data<T1, T2>(T1 messageName, string id, T2 data)
           where T1 : struct, Enum
           where T2 : unmanaged
        {
            return Data(Header.CreateHeader(messageName, id), Format.ToData(data));
        }

        public static byte[] Data<T>(T messageName, string id, ReadOnlySpan<byte> data)
           where T : struct, Enum
        {
            return Data(Header.CreateHeader(messageName, id), data);
        }

        public static byte[] Data<T>(byte[] header, T data)
            where T : unmanaged
        {
            return Data(header, Format.ToData(data));
        }

        public static byte[] Data(ReadOnlySpan<byte> header, ReadOnlySpan<byte> data)
        {
            return Format.Combine(header, data);
        }
    }
}
