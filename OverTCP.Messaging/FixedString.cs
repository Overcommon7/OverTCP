using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace OverTCP.Messaging
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct FixedString_8
    {
        [NonSerialized] const int Size = 8;
        public FixedString_8(string data)
        {
            Value = data;
        }

        private fixed byte mData[Size];
        public string Value
        {
            get
            {
                fixed (byte* ptr = mData)
                {
                    return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ptr, Size)).TrimEnd('\0');
                }

            }
            set
            {
#if DEBUG
                if (value.Length > Size)
                {
                    Console.WriteLine("Length Of String Is Larger Than Buffer Size");
                }
#endif
                fixed (byte* ptr = mData)
                {
                    Encoding.UTF8.GetBytes(value.AsSpan(0, Math.Min(value.Length, Size)), new Span<byte>(ptr, Size));
                }

            }
        }

        public static implicit operator string(FixedString_8 str) => str.Value;
        public static implicit operator FixedString_8(string value) => new() { Value = value };
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct FixedString_16
    {
        [NonSerialized] const int Size = 16;

        public FixedString_16(string data)
        {
            Value = data;
        }

        private fixed byte mData[Size];
        public string Value
        {
            get
            {
                fixed (byte* ptr = mData)
                {
                    return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ptr, Size)).TrimEnd('\0');
                }

            }
            set
            {
#if DEBUG
                if (value.Length > Size)
                {
                    Console.WriteLine("Length Of String Is Larger Than Buffer Size");
                }
#endif
                fixed (byte* ptr = mData)
                {
                    Encoding.UTF8.GetBytes(value.AsSpan(0, Math.Min(value.Length, Size)), new Span<byte>(ptr, Size));
                }
            }
        }

        public static implicit operator string(FixedString_16 str) => str.Value;
        public static implicit operator FixedString_16(string value) => new() { Value = value };
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct FixedString_32
    {
        [NonSerialized] const int Size = 32;
        public FixedString_32(string data)
        {
            Value = data;
        }

        private fixed byte mData[Size];
        public string Value
        {
            get
            {
                fixed (byte* ptr = mData)
                {
                    return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ptr, Size)).TrimEnd('\0');
                }

            }
            set
            {
#if DEBUG
                if (value.Length > Size)
                {
                    Console.WriteLine("Length Of String Is Larger Than Buffer Size");
                }
#endif
                fixed (byte* ptr = mData)
                {
                    Encoding.UTF8.GetBytes(value.AsSpan(0, Math.Min(value.Length, Size)), new Span<byte>(ptr, Size));
                }
            }
        }

        public static implicit operator string(FixedString_32 str) => str.Value;
        public static implicit operator FixedString_32(string value) => new() { Value = value };
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct FixedString_64
    {
        [NonSerialized] const int Size = 64;
        public FixedString_64(string data)
        {
            Value = data;
        }

        private fixed byte mData[Size];
        public string Value
        {
            get
            {
                fixed (byte* ptr = mData)
                {
                    return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ptr, Size)).TrimEnd('\0');
                }
                
            }
            set
            {
#if DEBUG
                if (value.Length > Size)
                {
                    Console.WriteLine("Length Of String Is Larger Than Buffer Size");
                }
#endif
                fixed (byte* ptr = mData)
                {
                    Encoding.UTF8.GetBytes(value.AsSpan(0, Math.Min(value.Length, Size)), new Span<byte>(ptr, Size));
                }
            }
        }

        public static implicit operator string(FixedString_64 str) => str.Value;
        public static implicit operator FixedString_64(string value) => new() { Value = value };
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct FixedString_128
    {
        [NonSerialized] const int Size = 128;

        public FixedString_128(string data)
        {
            Value = data;
        }

        private fixed byte mData[Size];
        public string Value
        {
            get
            {
                fixed (byte* ptr = mData)
                {
                    return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ptr, Size)).TrimEnd('\0');
                }

            }
            set
            {
#if DEBUG
                if (value.Length > Size)
                {
                    Console.WriteLine("Length Of String Is Larger Than Buffer Size");
                }
#endif
                fixed (byte* ptr = mData)
                {
                    Encoding.UTF8.GetBytes(value.AsSpan(0, Math.Min(value.Length, Size)), new Span<byte>(ptr, Size));
                }
            }
        }

        public static implicit operator string(FixedString_128 str) => str.Value;
        public static implicit operator FixedString_128(string value) => new() { Value = value };
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct FixedString_256
    {
        [NonSerialized] const int Size = 256;

        public FixedString_256(string data)
        {
            Value = data;
        }

        private fixed byte mData[Size];
        public string Value
        {
            get
            {
                fixed (byte* ptr = mData)
                {
                    return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ptr, Size)).TrimEnd('\0');
                }

            }
            set
            {
#if DEBUG
                if (value.Length > Size)
                {
                    Console.WriteLine("Length Of String Is Larger Than Buffer Size");
                }
#endif
                fixed (byte* ptr = mData)
                {
                    Encoding.UTF8.GetBytes(value.AsSpan(0, Math.Min(value.Length, Size)), new Span<byte>(ptr, Size));
                }
            }
        }

        public static implicit operator string(FixedString_256 str) => str.Value;
        public static implicit operator FixedString_256(string value) => new() { Value = value };
    }
}
