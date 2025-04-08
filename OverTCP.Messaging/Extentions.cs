using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OverTCP.Messaging
{
    public static class Extentions
    {
        public static bool IsNumericType<T>() where T : unmanaged
        {
            Type type = typeof(T);
            return type == typeof(byte) || type == typeof(sbyte) ||
                    type == typeof(short) || type == typeof(ushort) ||
                    type == typeof(int) || type == typeof(uint) ||
                    type == typeof(long) || type == typeof(ulong) ||
                    type == typeof(float) || type == typeof(double) ||
                    type == typeof(decimal) || type == typeof(bool);

        }

        public static ReadOnlySpan<byte> Pack<T>(this T arg0)
            where T : unmanaged
        {
#if DEBUG
        if (!IsNumericType<T>())
            throw new ArgumentException($"{typeof(T)} Must be Numeric");
#endif

            return MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref arg0, 1));
        }

        public static ReadOnlySpan<byte> Pack<T>(T arg0, T arg1)
            where T : unmanaged
        {
#if DEBUG
        if (!IsNumericType<T>())
            throw new ArgumentException($"{typeof(T)} Must be Numeric");
#endif

            return MemoryMarshal.Cast<T, byte>(new ReadOnlySpan<T>([arg0, arg1]));
        }

        public static ReadOnlySpan<byte> Pack<T>(T arg0, T arg1, T arg2)
            where T : unmanaged
        {
#if DEBUG
        if (!IsNumericType<T>())
            throw new ArgumentException($"{typeof(T)} Must be Numeric");
#endif

            return MemoryMarshal.Cast<T, byte>(new ReadOnlySpan<T>([arg0, arg1, arg2]));
        }

        public static ReadOnlySpan<byte> Pack<T>(T arg0, T arg1, T arg2, T arg3)
            where T : unmanaged
        {
#if DEBUG
        if (!IsNumericType<T>())
            throw new ArgumentException($"{typeof(T)} Must be Numeric");
#endif

            return MemoryMarshal.Cast<T, byte>(new ReadOnlySpan<T>([arg0, arg1, arg2, arg3]));
        }
        
    }
}
