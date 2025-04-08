using System;
using System.Collections.Generic;
using System.Text;

namespace OverTCP.Messaging
{
    public static class Header
    {
        public static ulong ALL_ULONG_ID = 255ul;
        public static int ALL_INT_ID = 255;
        public enum Style : int
        {
            IntegerID,
            ULongID,
            StringID,
            IArguments,
            Miscelaneous
        }
        public static byte[] CreateHeader<T>(T messageName, IArguments arguments)
            where T : struct, Enum
        {            
            byte[] byteData = arguments.ToByteData();
            int size = sizeof(int) + sizeof(int) + sizeof(int) + byteData.Length;
            byte[] data = new byte[size];
            Span<byte> buffer = new Span<byte>(data);

            if (!BitConverter.TryWriteBytes(buffer.Slice(0 , sizeof(int)), Convert.ToInt32(messageName)))
                throw new Exception("Could Not Convert Message To Int32");

            BitConverter.TryWriteBytes(buffer.Slice(sizeof(int), sizeof(int)), (int)Style.IArguments);
            BitConverter.TryWriteBytes(buffer.Slice(sizeof(int) * 2, sizeof(int)), byteData.Length);
            byteData.AsSpan().CopyTo(buffer.Slice(sizeof(int) * 3));
            return data;
        }

        public static byte[] CreateHeader<T>(T messageName, int id)
            where T : struct, Enum
        {
            int size = sizeof(int) * 3;
            byte[] data = new byte[size];
            Span<byte> buffer = new Span<byte>(data);
            BitConverter.TryWriteBytes(buffer.Slice(0, sizeof(int)), Convert.ToInt32(messageName));
            BitConverter.TryWriteBytes(buffer.Slice(sizeof(int), sizeof(int)), (int)Style.IntegerID);
            BitConverter.TryWriteBytes(buffer.Slice(sizeof(int) * 2), id);
            return data;
        }

        public static byte[] CreateHeader<T>(T messageName, ulong id)
            where T : struct, Enum
        {
            int size = sizeof(int) + sizeof(int) + sizeof(ulong);
            byte[] data = new byte[size];
            Span<byte> buffer = new Span<byte>(data);
            BitConverter.TryWriteBytes(buffer.Slice(0, sizeof(int)), Convert.ToInt32(messageName));
            BitConverter.TryWriteBytes(buffer.Slice(sizeof(int), sizeof(int)), (int)Style.ULongID);
            BitConverter.TryWriteBytes(buffer.Slice(sizeof(int) * 2), id);
            return data;
        }

        public static byte[] CreateHeader<T>(T messageName, string id)
            where T : struct, Enum
        {
            byte[] idAsBytes = Encoding.UTF8.GetBytes(id);
            int stringLengthInBytes = idAsBytes.Length;
            int size = sizeof(int) + sizeof(int) + sizeof(int) + stringLengthInBytes;
            byte[] data = new byte[size];
            Span<byte> buffer = new Span<byte>(data);
            BitConverter.TryWriteBytes(buffer.Slice(0, sizeof(int)), Convert.ToInt32(messageName));
            BitConverter.TryWriteBytes(buffer.Slice(sizeof(int), sizeof(int)), (int)Style.StringID);
            BitConverter.TryWriteBytes(buffer.Slice(sizeof(int) * 2, sizeof(int)), stringLengthInBytes);
            idAsBytes.CopyTo(buffer.Slice(sizeof(int) * 3));
            return data;
        }

        public static byte[] CreateHeader<T>(T messageName, params object[] arguments)
            where T : struct, Enum
        {
            List<byte> data = new(BitConverter.GetBytes(Convert.ToInt32(messageName)));
            data.AddRange(BitConverter.GetBytes((int)Style.Miscelaneous));
            foreach (var argument in arguments)
            {
                Type type = argument.GetType();
                byte[]? add = null;
                if (type.IsPrimitive)
                {
                    if (type == typeof(byte))
                    {
                        data.Add((byte)argument);
                        continue;
                    }
                    if (type == typeof(short))
                        add = BitConverter.GetBytes((short)argument);
                    else if (type == typeof(int))
                        add = BitConverter.GetBytes((int)argument);
                    else if (type == typeof(long))
                        add = BitConverter.GetBytes((long)argument);
                    else if (type == typeof(ulong))
                        add = BitConverter.GetBytes((ulong)argument);
                    else
                    {
                        string? str = argument.ToString();
                        if (!string.IsNullOrEmpty(str))
                            add = Encoding.UTF8.GetBytes(str);
                    }                        
                }
                else if (type.IsEnum)
                {
                    add = BitConverter.GetBytes(Convert.ToInt32(messageName));
                }
                else
                {
                    string? str = argument.ToString();
                    if (!string.IsNullOrEmpty(str))
                        add = Encoding.UTF8.GetBytes(str);
                }

                if (add is not null && add.Length > 0)
                    data.AddRange(add);
            }

            data.InsertRange(7, BitConverter.GetBytes(data.Count - 8));
            return data.ToArray();
        }

        
    }
}
