using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace OverTCP.Messaging
{
    public static class Format
    {
        public static bool ToStruct<T>(ReadOnlySpan<byte> bytes, out T value) where T : unmanaged
        {
            try
            {
                value = MemoryMarshal.Read<T>(bytes);
                return true;
            }
            catch (Exception)
            {
                value = default(T);
                return false;
            }
           
        }
        public static string ToString(ReadOnlySpan<byte> bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }
        public static ReadOnlySpan<byte> ToData<T>(T data) where T : unmanaged
        {
            try
            {
                return MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref data, 1));
            }
            catch (Exception)
            {
                return ReadOnlySpan<byte>.Empty;
            }
            
        }

        public static ReadOnlySpan<byte> StructArrayToData<T>(T[] structs) where T : unmanaged
            => StructArrayToData(structs.AsSpan());

        public static ReadOnlySpan<byte> StructArrayToData<T>(Span<T> structs) where T : unmanaged
        {
            try
            {
                return MemoryMarshal.AsBytes(structs);
            }
            catch (Exception)
            {

                return ReadOnlySpan<byte>.Empty;
            }
        }

        public static ReadOnlySpan<T> DataToStructArray<T>(ReadOnlySpan<byte> bytes) where T : unmanaged
        {
            try
            {
                return MemoryMarshal.Cast<byte, T>(bytes);
            }
            catch (Exception)
            {

                return [];
            }
        }

        public static byte[] ToData_NonBlittable<T>(T value) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(value, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return arr;
        }

        public static T ToStruct_NonBlittable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(byte[] arr) where T : struct
        {
            T str;
            int size = Marshal.SizeOf<T>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(arr, 0, ptr, size);
                str = Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return str;
        }

        public static byte[] StructArrayToData_NonBlittable<T>(T[] structs) where T : struct
        {
            if (structs.Length == 0) return Array.Empty<byte>();

            int structSize = Marshal.SizeOf<T>();
            int totalSize = structSize * structs.Length;
            byte[] result = new byte[totalSize];

            // Pin the array in memory
            GCHandle handle = GCHandle.Alloc(result, GCHandleType.Pinned);
            IntPtr ptr = handle.AddrOfPinnedObject();

            try
            {
                for (int i = 0; i < structs.Length; i++)
                {
                    Marshal.StructureToPtr(structs[i], ptr + (i * structSize), false);
                }
            }
            finally
            {
                handle.Free();
            }

            return result;
        }

        public static T[] DataToStructArray_NonBlittable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(byte[] bytes, int startIndex = 0) where T : struct
        {
            int structSize = Marshal.SizeOf<T>();
            if (bytes.Length % structSize != 0)
                throw new ArgumentException($"Byte array length must be a multiple of {structSize}.");

            int count = bytes.Length / structSize;
            T[] result = new T[count];

            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            IntPtr ptr = handle.AddrOfPinnedObject();

            try
            {
                for (int i = 0; i < count; i++)
                {
                    result[i] = Marshal.PtrToStructure<T>(ptr);
                    ptr += structSize;
                }
            }
            finally
            {
                handle.Free();
            }

            return result;
        }

        public static byte[] Combine(ReadOnlySpan<byte> data_1, ReadOnlySpan<byte> data_2)
        {
            byte[] data = new byte[data_1.Length + data_2.Length];
            Span<byte> dataSpan = new Span<byte>(data);
            data_1.CopyTo(dataSpan.Slice(0, data_1.Length));
            data_2.CopyTo(dataSpan.Slice(data_1.Length));
            return data;
        }
    }
}
