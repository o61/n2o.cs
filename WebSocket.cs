using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace N2O
{
    public enum Opcode : byte
    {
        Continuation,
        Text,
        Binary,
        Close = 8,
        Ping = 9,
        Pong = 10,
    }

    public static class WebSocket
    {
        public static void Serve(Socket sock)
        {
            Receive(sock);
        }

        public static void Receive(Socket sock)
        {
            Parse(sock);
        }

        public static void Parse(Socket sock)
        {
            var b0 = new byte[1];
            sock.Receive(b0);

            var isFinalFrame = (b0[0] & 128) != 0;
            var reservedBits = (b0[0] & 112);
            var opcode = (Opcode)(b0[0] & 15);

            Console.WriteLine($"*** isFinalFrame={isFinalFrame} opcode={opcode}");

            var b1 = new byte[1];
            sock.Receive(b1);

            var isMasked = (b1[0] & 128) != 0;
            var payloadLength = (b1[0] & 127);
            Console.WriteLine($"*** isMasked={isMasked} payloadLength={payloadLength}");

            if (payloadLength == 126) {
                var payloadLengthBytes = new byte[2];
                sock.Receive(payloadLengthBytes);
                payloadLength = ToLittleEndianInt(payloadLengthBytes);
            } else if (payloadLength == 127) {
                var payloadLengthBytes = new byte[8];
                sock.Receive(payloadLengthBytes);
                payloadLength = ToLittleEndianInt(payloadLengthBytes);
            }
            Console.WriteLine($"*** payloadLength={payloadLength}");

            var maskBytes = new byte[4];
            var payload = new byte[payloadLength];

            if (isMasked) sock.Receive(maskBytes);

            sock.Receive(payload);

            if (isMasked) payload = payload.Select((x, i) => (byte)(x ^ maskBytes[i % 4])).ToArray();

            var payloadStr = new UTF8Encoding(false, true).GetString(payload);
            Console.WriteLine($"*** {payload.Length} data={payloadStr}");

            Send(sock, payloadStr);
        }

        public static void Send(Socket sock, string message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var messageFrame = FrameData(messageBytes, Opcode.Text);
            sock.Send(messageFrame);
        }

        public static byte[] FrameData(byte[] payload, Opcode opcode)
        {
            var memoryStream = new MemoryStream();
            byte op = (byte)((byte)opcode + 128);

            memoryStream.WriteByte(op);

            if (payload.Length > UInt16.MaxValue) {
                memoryStream.WriteByte(127);
                var lengthBytes = ToBigEndianBytes<ulong>(payload.Length);
                memoryStream.Write(lengthBytes, 0, lengthBytes.Length);
            } else if (payload.Length > 125) {
                memoryStream.WriteByte(126);
                var lengthBytes = ToBigEndianBytes<ushort>(payload.Length);
                memoryStream.Write(lengthBytes, 0, lengthBytes.Length);
            } else {
                memoryStream.WriteByte((byte)payload.Length);
            }

            memoryStream.Write(payload, 0, payload.Length);

            return memoryStream.ToArray();
        }

        private static int ToLittleEndianInt(byte[] source)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(source);

            if (source.Length == 2) return BitConverter.ToUInt16(source, 0);

            if (source.Length == 8) return (int)BitConverter.ToUInt64(source, 0);

            throw new ArgumentException("Unsupported size");
        }

        public static byte[] ToBigEndianBytes<T>(int source)
        {
            byte[] bytes;

            var type = typeof(T);
            if (type == typeof(ushort))
                bytes = BitConverter.GetBytes((ushort)source);
            else if (type == typeof(ulong))
                bytes = BitConverter.GetBytes((ulong)source);
            else if (type == typeof(int))
                bytes = BitConverter.GetBytes(source);
            else
                throw new InvalidCastException("Cannot be cast to T");

            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }
    }
}
