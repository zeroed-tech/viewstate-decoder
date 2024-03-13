using System.Runtime.InteropServices;
using System.Text;

namespace viewstate_decoder
{
    public static class Utilities
    {
        public static int Read7BitEndodedInt(Stream input)
        {
            int num = 0;
            int num2 = 0;
            while (num2 != 35)
            {
                byte b = (byte)input.ReadByte();
                num |= (int)(b & 127) << num2;
                num2 += 7;
                if ((b & 128) == 0)
                {
                    return num;
                }
            }
            throw new FormatException("Invalid 7 bit int");
        }
    }
    public static class StreamExtensions
    {
        public static short ReadInt16(this Stream stream)
        {
            var sz = Marshal.SizeOf(typeof(short));
            var buffer = new byte[sz];
            stream.Read(buffer, 0, sz);
            return BitConverter.ToInt16(buffer, 0);
        }

        public static int ReadInt32(this Stream stream)
        {
            var sz = Marshal.SizeOf(typeof(int));
            var buffer = new byte[sz];
            stream.Read(buffer, 0, sz);
            return BitConverter.ToInt32(buffer, 0);
        }

        public static long ReadInt64(this Stream stream)
        {
            var sz = Marshal.SizeOf(typeof(long));
            var buffer = new byte[sz];
            stream.Read(buffer, 0, sz);
            return BitConverter.ToInt64(buffer, 0);
        }

        public static float ReadSingle(this Stream stream)
        {
            var sz = Marshal.SizeOf(typeof(float));
            var buffer = new byte[sz];
            stream.Read(buffer, 0, sz);
            return BitConverter.ToSingle(buffer, 0);
        }

        /// <summary>
        /// 2.1.1.6 LengthPrefixedString
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static string ReadVarString(this Stream stream)
        {
            var length = 0;
            bool lastByte = false;
            int position = 0;
            while (!lastByte)
            {
                var b = (byte)stream.ReadByte();
                length += ((b & 127) << position) /*0b01111111*/;
                position += 7;
                lastByte = (b & 128 /*0xb10000000*/) == 0;
            }
            var buffer = new byte[length];
            stream.Read(buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }

        public static byte Peek(this Stream stream)
        {
            var position = stream.Position;
            var b = (byte)stream.ReadByte();
            stream.Position = position;
            return b;
        }

        public static object ReadPrimitiveType(this Stream stream, PrimitiveTypeEnumeration t)
        {
            switch (t)
            {
                case PrimitiveTypeEnumeration.Boolean:
                    return stream.ReadByte();
                case PrimitiveTypeEnumeration.Byte:
                    return stream.ReadByte();
                case PrimitiveTypeEnumeration.Char:
                    return stream.ReadByte();
                case PrimitiveTypeEnumeration.Decimal:
                    return stream.ReadVarString();
                case PrimitiveTypeEnumeration.Double:
                    return stream.ReadInt64();
                case PrimitiveTypeEnumeration.Int16:
                    return stream.ReadInt16();
                case PrimitiveTypeEnumeration.Int32:
                    return stream.ReadInt32();
                case PrimitiveTypeEnumeration.Int64:
                    return stream.ReadInt64();
                case PrimitiveTypeEnumeration.SByte:
                    return stream.ReadByte();
                case PrimitiveTypeEnumeration.Single:
                    return stream.ReadSingle();
                case PrimitiveTypeEnumeration.TimeSpan:
                    return stream.ReadInt64();
                case PrimitiveTypeEnumeration.DateTime:
                    byte[] buffer = new byte[64];
                    stream.Read(buffer, 0, 64);
                    return buffer; // TODO
                case PrimitiveTypeEnumeration.UInt16:
                    return (UInt16)stream.ReadInt16();
                case PrimitiveTypeEnumeration.UInt32:
                    return (UInt32)stream.ReadInt32();
                case PrimitiveTypeEnumeration.UInt64:
                    return (UInt64)stream.ReadInt64();
                case PrimitiveTypeEnumeration.Null:
                    return null; // todo
                case PrimitiveTypeEnumeration.String:
                    return stream.ReadVarString();
                default:
                    return stream.ReadInt32();
            }
        }
    }
}
