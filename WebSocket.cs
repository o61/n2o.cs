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

            Send(sock, b0, b1, payloadLength, payload);
        }
        
        public static void Send(Socket sock, byte[] b0, byte[] b1, long payloadLength, byte[] data)
        {
            sock.Send(b0);
            sock.Send(b1);
            if (payloadLength <= 125) {
                sock.Send(data);
            } else if (payloadLength == 126) {
                var length = Convert.ToInt32(payloadLength);
                var lengthBytes = BitConverter.GetBytes(length);
                sock.Send(lengthBytes);
                sock.Send(data);
            } else if (payloadLength == 127) {
                var lengthBytes = BitConverter.GetBytes(payloadLength);
                sock.Send(lengthBytes);
                sock.Send(data);
            }
        }

        private static int ToLittleEndianInt(byte[] source)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(source);

            if (source.Length == 2) return BitConverter.ToUInt16(source, 0);

            if (source.Length == 8) return (int)BitConverter.ToUInt64(source, 0);

            throw new ArgumentException("Unsupported size");
        }
    }
}
