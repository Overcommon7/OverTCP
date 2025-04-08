using System;

namespace OverTCP.Messaging
{
    public interface IArguments
    {
        public byte[] ToByteData();
        public void FromBytes(Span<byte> data);
    }
}
