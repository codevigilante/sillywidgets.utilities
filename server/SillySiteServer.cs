using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using Amazon.Lambda.TestUtilities;

namespace SillyWidgets.Utilities.Server
{
    public class SillySiteServer
    {
        public SillyProxyApplication RequestHandler { get; private set; }
        public IPAddress IP { get; private set; }
        public int Port { get; private set; }

        private string TestPayload = "<html><body><h1>PLACEHOLDER</h1></body></html>";
        private string Error404 = "<html><body><p>404 - Not Found</p></body></html>";

        public SillySiteServer(SillyProxyApplication requestHandler, int port = 7575, IPAddress ip = null)
        {
            this.RequestHandler = requestHandler;
            this.IP = (ip == null) ? IPAddress.Loopback : ip;
            this.Port = (port <= 0) ? 7575 : port;
        }

        private TcpListener Listener = null;

        public async Task Start()
        {
            if (RequestHandler == null)
            {
                throw new Exception("Cannot start server, SillyProxyHandler cannot be null");
            }

            Console.WriteLine("Starting build server on " + IP.ToString() + ":" + Port + ":" + Directory.GetCurrentDirectory());

            Listener = new TcpListener(IP, Port);

            Listener.Start();

            while(true)
            {
                string consoleStr = string.Empty;
                Socket socket = null;
                SillyHttpResponse response = new SillyHttpResponse(SillyHttpStatusCode.Success, Encoding.ASCII.GetBytes(TestPayload), SillyMimeType.TextHtml);

                try
                {
                    socket = await Listener.AcceptSocketAsync();

                    if (socket == null)
                    {
                        throw new Exception("Incoming connection is null");
                    }
                    
                    consoleStr += socket.RemoteEndPoint + " -- [" + DateTime.Now.ToString() + "] ";

                    if (socket.Connected)
                    {
                        Byte[] receiveData = new Byte[1024];

                        socket.Receive(receiveData, receiveData.Length, 0);

                        string buffer = Encoding.ASCII.GetString(receiveData);

                        SillyHttpRequestParser request = new SillyHttpRequestParser(buffer);

                        if (request.Ignore)
                        {
                            consoleStr += "-> Empty request received, ignoring";

                            continue;
                        }

                        if (request.IsInvalid)
                        {
                            consoleStr += "-> " + request.InvalidReason + " ";

                            continue;
                        }

                        consoleStr += request.Method + " " + request.URL + " " + request.Version;// + " : PROXY " + request.httpMethod + " " + request.path + " " + request.QueryToString();

                        string normalizedRequest = Directory.GetCurrentDirectory() + request.URL.Trim().ToLower();

                        // figure out if request should be proxied or not. Probably be configurable by the user sometime in the future.

                        if (!File.Exists(normalizedRequest))
                        {
                            consoleStr += " : PROXY " + request.httpMethod + " " + request.path + " " + request.QueryToString();

                            TestLambdaContext lambdaContext = new TestLambdaContext();

                            response.ProxyResponse = RequestHandler.Handle(request, lambdaContext);
                        }
                        else
                        {
                            FileInfo requestedFile = new FileInfo(normalizedRequest);
                            SillyResource resource = new SillyResource(requestedFile);

                            response.Payload = resource.Contents();

                            switch(resource.Type)
                            {
                                case SillyResource.Types.CSS:
                                    response.ProxyResponse.headers.ContentType = SillyMimeType.TextCss;
                                    break;
                                case SillyResource.Types.JS:
                                    response.ProxyResponse.headers.ContentType = SillyMimeType.ApplicationJavascript;
                                    break;
                                default:
                                    break;
                            }
                        }

                        consoleStr += " RESOLVED ";
                    }
                }
                catch(Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    consoleStr += "-> Exception: " + ex.Message + " ";
                }
                finally
                {
                    response.SendResponse(socket);
                    
                    consoleStr += response.ProxyResponse.statusCode;

                    Console.WriteLine(consoleStr);
                    Console.ResetColor();

                    if (socket != null && socket.Connected)
                    {
                        socket.Dispose();
                    }
                }
            }
        }
    }
}