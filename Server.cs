using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
        public readonly string Path;
        public readonly Dictionary<string, string> Headers;
        public Req() {}
        public Req(string path, Dictionary<string, string> headers) {
            IsValid = true;
            Path = path;
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
            var tokens = req.Split(new [] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return new Req();

            var header = tokens[0].Split(new [] {" "}, StringSplitOptions.RemoveEmptyEntries);
            if (header.Length < 1) return new Req();

            var method = header[0];
            var path = header[1];
            
            var xs = tokens.Skip(1);
            return method == "GET" ? new Req(path, ParseHeaders(tokens.Skip(1))) : new Req();
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

            if (!req.IsValid) {
                BadRequest(sock);
                return;
            }

            var reqPath = req.Path == "/"
                          ? "/static/html/index.html"
                          : req.Path.StartsWith("/ws")
                            ? req.Path.Substring(3)
                            : req.Path;

            var filePath = $"./{reqPath}";
            Console.WriteLine($"*** filePath={filePath}");
            if (!File.Exists(filePath)) {
                NotFound(sock);
                return;
            }

            var fileContent = File.ReadAllBytes(filePath);
            var resp = new Resp(HttpStatusCode.OK,
                                new Dictionary<string, string> () {
                                    {"Content-Type",  "text/html"},
                                    {"Content-Length", fileContent.Length.ToString()}},
                                fileContent);
            SendResp(sock, resp);
        }

        private static void BadRequest(Socket sock) {
            SendError(sock, HttpStatusCode.BadRequest);
        }

        private static void NotFound(Socket sock) {
            SendError(sock, HttpStatusCode.NotFound);
        }

        private static void SendError(Socket sock, HttpStatusCode code) {
            SendResp(sock, new Resp(code));
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