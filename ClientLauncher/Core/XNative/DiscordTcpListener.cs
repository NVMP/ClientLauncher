using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;

namespace ClientLauncher.Core.XNative
{
    /// <summary>
    /// This opens up a socket locally to handle authentication handshakes. This code is not going to remain here for long, 
    /// in future protocol based authorization and hand-off will be more secure and more compatible for most players.
    /// </summary>
    public class DiscordTcpListener
    {
        public static readonly int ClientDiscordAuthListener = 8085;
        protected HttpListener InternalServer;
        protected Thread InternalThread;

        protected CancellationTokenSource CancellationToken;

        public class AuthorizationReceivedArgs : EventArgs
        {
            public HttpListenerResponse Response { get; set; }
            public string ServerClientID { get; set; }
            public string ServerAuthResponse { get; set; }
        }

        public delegate void AuthorizationReceivedEventHandler(AuthorizationReceivedArgs e);

        public AuthorizationReceivedEventHandler DiscordAuthReceived;

        public DiscordTcpListener()
        {
            CancellationToken = new CancellationTokenSource();
        }

        public void AttemptServerCreation(int port)
        {
            Trace.WriteLine($"Trying port {port}");
            InternalServer = new HttpListener();
            InternalServer.Prefixes.Add($"http://localhost:{port}/");
            InternalServer.Start();
        }

        protected void Receive()
        {
            while (!CancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Will wait here until we hear from a connection
                    HttpListenerContext ctx = InternalServer.GetContext();

                    // Peel out the requests and response objects
                    HttpListenerRequest req = ctx.Request;
                    HttpListenerResponse resp = ctx.Response;

                    if (req.Url.AbsolutePath.StartsWith("/discord"))
                    {
                        var args = new AuthorizationReceivedArgs
                        {
                            Response = resp,
                            ServerClientID = req.QueryString.Get("server"),
                            ServerAuthResponse = req.QueryString.Get("response")
                        };

                        if (args.ServerAuthResponse != null && 
                            args.ServerClientID != null &&
                            DiscordAuthReceived != null)
                        {
                            DiscordAuthReceived.Invoke(args);
                        }
                    }

                    // Write the response info
                    byte[] data = Encoding.UTF8.GetBytes("<!doctype html><head><title>NV:MP</title></head><body><script>setTimeout(()=>window.close(), 250);window.history.replaceState({}, document.title, \"/\");</script><div><b>NV:MP Launcher</b></div>You can close this window and return to the launcher!</body>");
                    resp.ContentType = "text/html";
                    resp.ContentEncoding = Encoding.UTF8;
                    resp.ContentLength64 = data.LongLength;

                    // Write out to the response stream (asynchronously), then close it
                    resp.OutputStream.Write(data, 0, data.Length);
                    resp.Close();
                }
                catch (Exception)
                {

                }
            }
        }

        public bool Initialize()
        {
            int attempts = 5;
            var rand = new Random();
            while (attempts > 0)
            {
                try
                {
                    AttemptServerCreation(ClientDiscordAuthListener);

                    InternalThread = new Thread(Receive);
                    InternalThread.Start();
                    return true;
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Localserver startup failure " + e.Message);
                }

                attempts--;
            }

            if (attempts == 0)
            {
                MessageBox.Show($"Could not start up the NV:MP Discord Tcp Listener, servers with Discord authentication may fail! Please make sure port {ClientDiscordAuthListener} is open!", "New Vegas: Multiplayer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return false;
        }

        public void Shutdown()
        {
            CancellationToken.Cancel();

            if (InternalServer != null)
            {
                InternalServer.Close();
            }

            if (InternalThread != null)
            {
                InternalThread.Abort();
                InternalThread.Join();
                InternalThread = null;
            }
        }
    }
}
