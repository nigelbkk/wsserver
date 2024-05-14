using WSServer;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;

[assembly: OwinStartup(typeof(Program.Startup))]
namespace WSServer
{
    class Program
    {
        static IDisposable SignalR;

        static void Main(string[] args)
        {
            string url = "http://88.202.230.157:8088";
           // url = "http://*:8088";
            SignalR = WebApp.Start(url);
            Console.WriteLine("Waiting for connections on:  " + url);
            Console.ReadKey();
        }
        public class Startup
        {
            public void Configuration(IAppBuilder app)
            {
                app.MapSignalR();
            }
        }
        [HubName("WebSocketsHub")]
        public class MyHub : Hub
        {
            public static HashSet<string> ConnectedIds = new HashSet<string>();
            private StreamingAPI streamingAPI = null;
            private void ConnectStreamingAPI()
            {
                Settings settings = Settings.DeSerialize();
                streamingAPI = new StreamingAPI(settings.AppID, settings.Account, settings.Password, settings.Cert, settings.CertPassword);
                streamingAPI.OrdersCallback += (String json1, String json2, String json3) =>
                {
                    Debug.WriteLine("OrdersCallback");
                    Clients.All.ordersChanged(json1, json2, json3);
                };
                streamingAPI.SubscribeOrders();
            }
            public override Task OnConnected()
            {
                object ipAddress;

                if (streamingAPI == null)
                {
                    ConnectStreamingAPI();
                }
                Context.Request.Environment.TryGetValue("server.RemoteIpAddress", out ipAddress);
                Console.WriteLine(DateTime.UtcNow.ToString("HH:mm:ss") + " " + ipAddress + " connected");
                ConnectedIds.Add(Context.ConnectionId);
                return base.OnConnected();
            }
            public override Task OnReconnected()
            {
                object ipAddress;
                Context.Request.Environment.TryGetValue("server.RemoteIpAddress", out ipAddress);
                Console.WriteLine(DateTime.UtcNow.ToString("HH:mm:ss") + " " + ipAddress + " reconnected");
                ConnectedIds.Add(Context.ConnectionId);
                return base.OnReconnected();
            }
            public override Task OnDisconnected(bool stopCalled)
            {
                try
                {
                    object ipAddress;
                    Context.Request.Environment.TryGetValue("server.RemoteIpAddress", out ipAddress);
                    Console.WriteLine(DateTime.UtcNow.ToString("HH:mm:ss") + " " + ipAddress + " disconnected");
                }
                catch (Exception)
                {
                    Console.WriteLine(Context.ConnectionId + " disconnected");
                }
                ConnectedIds.Remove(Context.ConnectionId);
                if (ConnectedIds.Count == 0)
                {
//                    streamingAPI = null;
                }
                return base.OnDisconnected(stopCalled);
            }
            public void Send(string message)
            {
                Clients.All.Message(message);
            }
            public void SubscribeMarket(string marketid)
            {
                Console.WriteLine(Context.ConnectionId + " subcribed to market " + marketid);
                streamingAPI.SubscribeMarket(marketid);
            }
        }
    }
}