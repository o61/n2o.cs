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
        // Received data string.
        public StringBuilder Sb = new StringBuilder();
    }

    public class Resp {
        public int Status;
        public Dictionary<string, string> Headers;
        public byte[] Body;
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
            var bytesRead = sock.EndReceive(ar);
            
            Console.WriteLine($"*** bytesRead={bytesRead}");

            if (bytesRead <= 0) return;

            // There  might be more data, so store the data received so far.
            state.Sb.Append(Encoding.UTF8.GetString(state.Buffer, 0, bytesRead));

            // Check for end-of-file tag. If it is not there, read 
            // more data.
            var content = state.Sb.ToString();
            Console.WriteLine("*** Read {0} bytes from socket. \n Data : {1}", content.Length, content);

            var tokens = content.Split(new [] {"\r\n"}, StringSplitOptions.None);
            if (tokens.Length == 0) {
                BadRequest(sock);
                return;
            }

            Console.WriteLine($"*** token={tokens[0]}");
            var header = tokens[0].Split(new [] {" "}, StringSplitOptions.None);
            if (header.Length < 1) {
                BadRequest(sock);
                return;
            }

            Console.WriteLine($"*** header={header[0]}, {header[1]}");
            var method = header[0];
            var path = header[1];
            var filePath = "static/html/" + path + ".html";
            Console.WriteLine($"*** filePath={filePath}");
            if (!File.Exists(filePath)) {
                NotFound(sock);
                return;
            }

            var fileContent = File.ReadAllText(filePath, Encoding.UTF8);
            var r = new Resp {Status = 200, 
                              Headers = new Dictionary<string, string> (){
                                      {"Content-Type", "text/html"},
                                      {"Content-Length", Encoding.UTF8.GetBytes(fileContent).Length.ToString()}
                                  }
                             };
            var resp = "HTTP/1.1 200 OK\r\n" + String.Join("\r\n", r.Headers.Select(x => x.Key + ": " + x.Value)) + "\r\n\r\n" + fileContent;
            Console.WriteLine($"*** resp={resp}");

            var respBytes = Encoding.UTF8.GetBytes(resp);
            // Echo the data back to the client.
            sock.BeginSend(respBytes, 0, respBytes.Length, 0, Send, sock);
        }

        private static void BadRequest(Socket sock) {
            var resp = Encoding.UTF8.GetBytes("HTTP/1.1 400 Bad Request\r\n");
            sock.BeginSend(resp, 0, resp.Length, 0, Send, sock);
        }

        private static void NotFound(Socket sock) {
            var resp = Encoding.UTF8.GetBytes("HTTP/1.1 404 Not Found\r\n");
            sock.BeginSend(resp, 0, resp.Length, 0, Send, sock);
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