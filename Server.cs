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
    // State object for reading client data asynchronously
    public class StateObject
    {
        // Client  socket.
        public Socket WorkSocket;
        // Size of receive buffer.
        public const int BufferSize = 2048;
        // Receive buffer.
        public byte[] Buffer = new byte[BufferSize];
    }

    public class Req {
        public readonly bool IsValid;
        public readonly HttpMethod Cmd;
        public readonly string Path;
        public readonly string Vers;
        public readonly Dictionary<string, string> Headers;

        public Req() {}

        public Req(string path, string vers, Dictionary<string, string> headers) {
            IsValid = true;
            Cmd = HttpMethod.Get;
            Path = path;
            Vers = vers;
            Headers = headers;
        }
    }

    public class Resp {
        public readonly HttpStatusCode Status;
        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();
        public readonly byte[] Body = new byte[] {};

        public Resp(HttpStatusCode status) {
            Status = status;
        }

        public Resp(HttpStatusCode status, Dictionary<string, string> headers, byte[] body) {
            Status = status;
            Headers = headers;
            Body = body;
        }
    }

    public static class Server {
        // Thread signal.
        private static readonly ManualResetEvent AllDone = new ManualResetEvent(false);

        public static void Run() {
            // Establish the local endpoint for the socket.
            var ipHostInfo = Dns.GetHostEntry("localhost");
            var ipAddress = ipHostInfo.AddressList[0];
            var localEndPoint = new IPEndPoint(ipAddress, 8989);

            // Create a TCP/IP socket.
            var listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try {
                listener.Bind(localEndPoint);
                listener.Listen(5);

                AcceptLoop(listener);
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();
        }

        private static void AcceptLoop(Socket serverSock)
        {
            while (true)
            {
                // Set the event to nonsignaled state.
                AllDone.Reset();

                // Start an asynchronous socket to listen for connections.
                Console.WriteLine("*** n2o server is waiting for a connection...");
                serverSock.BeginAccept(Accept, serverSock);

                // Wait until a connection is made before continuing.
                AllDone.WaitOne();
            }
        }

        private static Dictionary<string, string> ParseHeaders(IEnumerable<string> lns) {
            return lns.ToDictionary(x => x.Split(": ")[0], x => x.Split(": ")[1]);
        }

        private static Req ParseReq(string req) {
            Console.WriteLine($"*** req={req}");
            var headers = req.Split(new [] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);
            if (headers.Length == 0) return new Req();

            var header = headers[0].Split(new [] {" "}, StringSplitOptions.RemoveEmptyEntries);
            if (header.Length < 1) return new Req();

            var method = header[0];
            var path = header[1];
            var vers = header[2];
            return method == "GET" ? new Req(path, vers, ParseHeaders(headers.Skip(1))) : new Req();
        }

        private static void Accept(IAsyncResult ar) {
            Console.WriteLine("*** Accept");
            // Signal the main thread to continue.
            AllDone.Set();

            // Get the socket that handles the client request.
            var sock = (Socket) ar.AsyncState;
            var handler = sock.EndAccept(ar);

            // Create the state object.
            var state = new StateObject {WorkSocket = handler};
            handler.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, Receive, state);
        }

        private static string Router(string path) {
            switch (path) {
                case "/": return "static/html/index.html";
                default:  return path.Substring(path.StartsWith("/ws") ? 3 : 1);
            }
        }

        private static bool NeedUpgrade(Req req) {
            return req.Headers.Any(x => x.Key == "Upgrade" && x.Value.ToUpper() == "WEBSOCKET");
        }

        private static string GetKey(Socket sock, Req req) {
            if (!req.Headers.ContainsKey("Sec-WebSocket-Key")) {
                BadRequest(sock, "No Sec-WebSocket-Key header");
                return null;
            }
            var keyStr = req.Headers["Sec-WebSocket-Key"];
            const string magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            var keyBytes = Encoding.UTF8.GetBytes(keyStr + magic);
            using (var sha = new SHA1CryptoServiceProvider()) {
                var keyEncrypted = sha.ComputeHash(keyBytes);
                return Convert.ToBase64String(keyEncrypted);
            }
        }

        private static void AssertHandshake(Socket sock, Req req) {
            if (req.Cmd != HttpMethod.Get) {
                BadRequest(sock, "Method must be GET");
                return;
            }

            if (req.Vers != "HTTP/1.1") {
                BadRequest(sock, "HTTP version must be 1.1");
                return;
            }

            if (!req.Headers.ContainsKey("Sec-WebSocket-Version") || req.Headers["Sec-WebSocket-Version"] != "13") {
                BadRequest(sock, "WebSocket version must be 13");
                return;
            }
        }

        private static Resp Upgrade(Socket sock, Req req) {
            AssertHandshake(sock, req);
            return new Resp(HttpStatusCode.SwitchingProtocols,
                                new Dictionary<string, string> () {
                                    {"Upgrade",  "websocket"},
                                    {"Connection", "Upgrade"},
                                    {"Sec-WebSocket-Accept", GetKey(sock, req)}},
                                new byte[]{});
        }

        private static void Receive(IAsyncResult ar) {
            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            var state = (StateObject) ar.AsyncState;
            var sock = state.WorkSocket;

            // Read data from the client socket. 
            var reqLength = sock.EndReceive(ar);
            Console.WriteLine($"*** Received {reqLength} bytes from socket.");
            if (reqLength <= 0) {
                BadRequest(sock);
                return;
            };

            var reqStr = Encoding.UTF8.GetString(state.Buffer, 0, reqLength);
            var req = ParseReq(reqStr);
            
            if (NeedUpgrade(req)) {
                var wsResp = Upgrade(sock, req);
                return;
            }

            if (!req.IsValid) {
                BadRequest(sock);
                return;
            }

            var reqPath = Router(req.Path);
            if (!File.Exists(reqPath)) {
                NotFound(sock);
                return;
            }

            var fileContent = File.ReadAllBytes(reqPath);
            var resp = new Resp(HttpStatusCode.OK,
                                new Dictionary<string, string> () {
                                    {"Content-Type",  "text/html"},
                                    {"Content-Length", fileContent.Length.ToString()}},
                                fileContent);
            SendResp(sock, resp);
        }

        private static void BadRequest(Socket sock, string body = "") {
            SendError(sock, HttpStatusCode.BadRequest);
        }

        private static void NotFound(Socket sock) {
            SendError(sock, HttpStatusCode.NotFound);
        }

        private static void SendError(Socket sock, HttpStatusCode code, string body = "") {
            SendResp(sock, new Resp(code, new Dictionary<string, string>(), Encoding.UTF8.GetBytes(body)));
        }

        private static void SendResp(Socket sock, Resp resp) {
            Console.WriteLine("*** Send");
            var respHeadersStr   = $"HTTP/1.1 {resp.Status} {nameof(resp.Status)}\r\n" +
                                   String.Join("\r\n", resp.Headers.Select(x => x.Key + ": " + x.Value)) + "\r\n\r\n";
            var respHeadersBytes = Encoding.UTF8.GetBytes(respHeadersStr);
            var respBytes = new byte[respHeadersBytes.Length + resp.Body.Length];
            Buffer.BlockCopy(respHeadersBytes, 0, respBytes, 0,                       respHeadersBytes.Length);
            Buffer.BlockCopy(resp.Body,        0, respBytes, respHeadersBytes.Length, resp.Body.Length);
            sock.BeginSend(respBytes, 0, respBytes.Length, 0, Send, sock);
        }

        private static void Send(IAsyncResult ar) {
            try {
                // Retrieve the socket from the state object.
                var handler = (Socket) ar.AsyncState;

                // Complete sending the data to the remote device.
                var bytesSent = handler.EndSend(ar);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();

            } catch (Exception e) {
                Console.WriteLine(e.ToString());
            }
        }
    }
}