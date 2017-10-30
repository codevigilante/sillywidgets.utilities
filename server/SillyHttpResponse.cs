using System;
using System.Text;
using System.Net.Sockets;
using System.Net.Http;

namespace SillyWidgets.Utilities.Server
{
    public class SillyHttpResponse
    {
        public string Version { get; set; }
        public Byte[] Payload { get; set; }
        public SillyProxyResponse ProxyResponse
        { 
            get { return _proxy; } 
            set
            {
                _proxy = (value == null) ? new SillyProxyResponse() : value;

                this.Payload = Encoding.ASCII.GetBytes(_proxy.body);
            } 
        }

        private SillyProxyResponse _proxy = null;

        public SillyHttpResponse(SillyHttpStatusCode code, Byte[] payload, string mimeType)
        {
            Version = "HTTP/1.1";
            Payload = payload;

            _proxy = new SillyProxyResponse();
            _proxy.headers.ContentType = mimeType;
        }
        
        private void Send(Socket socket, Byte[] data)
        {
            if (socket == null || !socket.Connected)
            {
                return;
            }

            try
            {
                int bytesSent = socket.Send(data, data.Length, 0);

                if (bytesSent == -1)
                {
                    throw new Exception("Something went wrong sending the response");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error responding: " + ex.Message);
            }
        }

        public void SendResponse(Socket socket)
        {
            int length = 0;
            byte[] data = new byte[0];

            if (Payload != null)
            {
                length = Payload.Length;
                data = Payload;
            }

            SendHeader(socket, length);
            Send(socket, data);
        }

        private void SendHeader(Socket socket, int contentSize)
        {
            if (socket == null)
            {
                return;
            }

            string buffer = Version + " " + ProxyResponse.statusCode + "\r\n";
            buffer += "Server: silly server v0.1\r\n";
            buffer += "Content-Type: " + ProxyResponse.headers.ContentType + "\r\n";
            buffer += "Accept-Ranges: bytes\r\n";
            buffer += "Content-Length: " + contentSize + "\r\n\r\n";

            Byte[] data = Encoding.ASCII.GetBytes(buffer);

            Send(socket, data);
        }
    }
}