using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OverTCP.Messaging
{
    public static class Extentions
    {
        public static ReadOnlySpan<byte> Pack<T>(this T arg0)
            where T : unmanaged, INumber<T>
        {
            return MemoryMarshal.Cast<T, byte>(new ReadOnlySpan<T>([arg0]));
        }

        public static ReadOnlySpan<byte> Pack<T>(T arg0, T arg1)
            where T : unmanaged, INumber<T>
        {
            return MemoryMarshal.Cast<T, byte>(new ReadOnlySpan<T>([arg0, arg1]));
        }

        public static ReadOnlySpan<byte> Pack<T>(T arg0, T arg1, T arg2)
            where T : unmanaged, INumber<T>
        {
            return MemoryMarshal.Cast<T, byte>(new ReadOnlySpan<T>([arg0, arg1, arg2]));
        }

        public static ReadOnlySpan<byte> Pack<T>(T arg0, T arg1, T arg2, T arg3)
            where T : unmanaged, INumber<T>
        {
            return MemoryMarshal.Cast<T, byte>(new ReadOnlySpan<T>([arg0, arg1, arg2, arg3]));
        }
        
    }
}
