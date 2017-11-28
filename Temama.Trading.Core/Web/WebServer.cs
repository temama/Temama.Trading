using System;
using System.Net;
using System.Threading;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace Temama.Trading.Core.Web
{
    public class WebServer
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly Func<HttpListenerRequest, string> _responderMethod;

        public WebServer(int port, string[] prefixes, Func<HttpListenerRequest, string> method)
        {
            if (!HttpListener.IsSupported)
                throw new NotSupportedException(
                    "Needs Windows XP SP2, Server 2003 or later.");

            // URI prefixes are required, for example 
            // "http://localhost:8080/index/".
            if (prefixes == null || prefixes.Length == 0)
                throw new ArgumentException("prefixes");

            // A responder method is required
            if (method == null)
                throw new ArgumentException("method");

            var ipAddr = "127.0.0.1";
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                var addr = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                if (addr != null)
                {
                    ipAddr = addr.ToString();
                }
            }


            foreach (string s in prefixes)
            {
                _listener.Prefixes.Add(string.Format("http://{0}:{1}{2}", System.Environment.MachineName, port, s));
                _listener.Prefixes.Add(string.Format("http://localhost:{0}{1}", port, s));
                _listener.Prefixes.Add(string.Format("http://{0}:{1}{2}", ipAddr, port, s));
                _listener.Prefixes.Add(string.Format("http://*:{0}{1}", port, s));
            }

            _responderMethod = method;
            _listener.Start();
        }

        public WebServer(Func<HttpListenerRequest, string> method, int port, params string[] prefixes)
            : this(port, prefixes, method) { }

        public void Run()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                Console.WriteLine("Webserver running...");
                try
                {
                    while (_listener.IsListening)
                    {
                        ThreadPool.QueueUserWorkItem((c) =>
                        {
                            var ctx = c as HttpListenerContext;
                            try
                            {
                                string rstr = _responderMethod(ctx.Request);
                                byte[] buf = Encoding.UTF8.GetBytes(rstr);
                                ctx.Response.ContentLength64 = buf.Length;
                                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                            }
                            catch { } // suppress any exceptions
                            finally
                            {
                                // always close the stream
                                ctx.Response.OutputStream.Close();
                            }
                        }, _listener.GetContext());
                    }
                }
                catch { } // suppress any exceptions
            });
        }

        public void Stop()
        {
            _listener.Stop();
            _listener.Close();
        }
    }
}