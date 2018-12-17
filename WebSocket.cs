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
            var x = sock.Receive(b0);
            Console.WriteLine($"*** x={x}");
        }
    }
}