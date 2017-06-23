using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace WebServerTask
{
    public sealed class HttpServer : IDisposable
    {
        private const string html = "<!DOCTYPE html> <html> <head> <style> .button { background-color: #4CAF50; border: none; color: white; padding: 15px 32px; text-align: center; text-decoration: none; display: inline-block; font-size: 16px; margin: 4px 2px; cursor: pointer; } .button1 {background-color: #ff0000;} .button2 {background-color: #00ff00;} .button3 {background-color: #ffff00;} .button4 {background-color: #e7e7e7; color: black;} .button5 {background-color: #555555;} </style> </head> <body> <h2>Światełka nadziei</h2> <a href=\"quietmaker.html? state = redon\" class=\"button button1\">Red LED</a> <a href=\"quietmaker.html? state = yellowon\" class=\"button button3\">Yellow LED</a> <a href=\"quietmaker.html? state = greenon\" class=\"button button2\">Green LED</a> <a href=\"quietmaker.html? state = redon & yellowon\" class=\"button button4\">Red & Yellow</a> </body> </html>";
        private const uint bufferSize = 8192;
        private readonly int port = 8000;
        private readonly StreamSocketListener listener;
        private readonly AppServiceConnection appServiceConnection;
        private WebServerBackgroundTask webServerBackgroundTask;

        public HttpServer(int serverPort, AppServiceConnection connection)
        {
            listener = new StreamSocketListener();
            port = serverPort;
            appServiceConnection = connection;
            listener.ConnectionReceived += (s, e) => ProcessRequestAsync(e.Socket);
        }

        public async void StartServer()
        {
            await listener.BindServiceNameAsync(port.ToString());
        }

        public void Dispose()
        {
            listener.Dispose();
        }

        private async void ProcessRequestAsync(StreamSocket socket)
        {
            StringBuilder request = new StringBuilder();
            using (IInputStream input = socket.InputStream)
            {
                byte[] data = new byte[bufferSize];
                IBuffer buffer = data.AsBuffer();
                uint dataRead = bufferSize;
                while (dataRead == bufferSize)
                {
                    await input.ReadAsync(buffer, bufferSize, InputStreamOptions.Partial);
                    request.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                    dataRead = buffer.Length;
                }
            }

            using (IOutputStream output = socket.OutputStream)
            {
                string requestMethod = request.ToString().Split('\n')[0];
                string[] requestParts = requestMethod.Split(' ');

                if (requestParts[0] == "GET")
                {
                    await WriteResponseAsync(requestParts[1], output);
                }
                else
                {
                    throw new InvalidDataException("HTTP method not supported: "
                                                   + requestParts[0]);
                }
            }
        }

        private async Task WriteResponseAsync(string request, IOutputStream os)
        {
            string state = String.Empty;
            bool stateChanged = false;

            if (request.Contains("redon"))
            {
                state += " Red";
                stateChanged = true;
            }
            if (request.Contains("yellowon"))
            {
                state += " Yellow";
                stateChanged = true;
            }
            if (request.Contains("greenon"))
            {
                state += " Green";
                stateChanged = true;
            }

            if (stateChanged)
            {
                var updateMessage = new ValueSet();
                updateMessage.Add("State", state);
                var responseStatus = await appServiceConnection.SendMessageAsync(updateMessage);
            }

            using (Stream resp = os.AsStreamForWrite())
            {
                byte[] bodyArray = Encoding.UTF8.GetBytes(html);
                MemoryStream stream = new MemoryStream(bodyArray);
                string header = String.Format("HTTP/1.1 200 OK\r\n" +
                                  "Content-Length: {0}\r\n" +
                                  "Connection: close\r\n\r\n",
                                  stream.Length);
                byte[] headerArray = Encoding.UTF8.GetBytes(header);
                await resp.WriteAsync(headerArray, 0, headerArray.Length);
                await stream.CopyToAsync(resp);
                await resp.FlushAsync();
            }
        }
    }
}
