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

namespace n2o
{
    public enum Opcode {
        ContinuationFrame,
        TextFrame,
        BinaryFrame,
        NonControlFrame3,
        NonControlFrame4,
        NonControlFrame5,
        NonControlFrame6,
        NonControlFrame7,
        ConnectionClose,
        Ping,
        Pong,
        ControlFrameB,
        ControlFrameC,
        ControlFrameD,
        ControlFrameE,
        ControlFrameF
    }
    
    public static class WebSocket {
        public static void Serve(Socket sock) {
            Receive(sock);
        }

        public static void Receive(Socket sock) {
            Parse(sock);
        }

        public static void Parse(Socket sock) {
            var b0 = new byte[1];
            sock.Receive(b0);
            var isFinalFrame = (b0[0] >> 0) & 1;
            var rsv1         = (b0[0] >> 1) & 1;
            var rsv2         = (b0[0] >> 2) & 1;
            var rsv3         = (b0[0] >> 3) & 1;
            var opcode       = (b0[0] >> 4) & 15;
            Console.WriteLine($"*** isFinalFrame={isFinalFrame} opcode={opcode}");

            var b1 = new byte[1];
            sock.Receive(b1);

            var isMasked       = (b1[0] >> 0) & 1;
            long payloadLength = (b1[0] >> 0) & 127;
            Console.WriteLine($"*** isMasked={isMasked} payloadLength={payloadLength}");

            if (payloadLength == 126) {
                var payloadLengthBytes = new byte[2];
                sock.Receive(payloadLengthBytes);
                payloadLength = BitConverter.ToInt64(payloadLengthBytes, 0);
            } else if (payloadLength == 127) {
                var payloadLengthBytes = new byte[8];
                sock.Receive(payloadLengthBytes);
                payloadLength = BitConverter.ToInt64(payloadLengthBytes, 0);
            }
            Console.WriteLine($"*** payloadLength={payloadLength}");

            var mask = new byte[4];
            var data = new byte[payloadLength];

            if (isMasked == 1) {
                sock.Receive(mask);
                sock.Receive(data);
                for (int i = 0; i < data.Length; i++) {
                    data[i] = Convert.ToByte(data[i] ^ mask[i % 4]);
                }
            } else {
                sock.Receive(data);
            }

            var dataStr = Encoding.UTF8.GetString(data);
            Console.WriteLine($"*** {data.Length} data={dataStr}");

            Send(sock, b0, b1, payloadLength, data);
        }
        
        public static void Send(Socket sock, byte[] b0, byte[] b1, long payloadLength, byte[] data) {
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
    }
}
