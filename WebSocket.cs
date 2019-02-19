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
            var fin    = (b0[0] >> 0) & 1;
            var rcv1   = (b0[0] >> 1) & 1;
            var rcv2   = (b0[0] >> 2) & 1;
            var rcv3   = (b0[0] >> 3) & 1;
            var opcode = (b0[0] >> 4) & 15;
            
            var b1 = new byte[1];
            sock.Receive(b1);
            var mask       = (b1[0] >> 0) & 1;
            var payloadLen = (b1[0] >> 0) & 127;

            Console.WriteLine($"*** fin={fin} {rcv1} {rcv2} {rcv3} opcode={opcode}");
            Console.WriteLine($"*** mask={mask} payloadLen={payloadLen}");
        }
    }
}