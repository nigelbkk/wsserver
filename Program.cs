using WSServer;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Collections.Generic;

[assembly: OwinStartup(typeof(Program.Startup))]
namespace WSServer
{
    class Program
    {
        //public StreamUpdateDelegate OrdersEventSink = null;
        //public StreamUpdateDelegate MarketEventSink = null;
        static IDisposable SignalR;

        static void Main(string[] args)
        {
            string url = "http://88.202.183.202:8088";
            //url = "http://192.168.1.6:8088";
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
            static int idx = 0;
            private static System.Timers.Timer timer = null;
            public MyHub()
            {
            }
            private void ConnectStreamingAPI()
            {
                Settings settings = Settings.DeSerialize();
                streamingAPI = new StreamingAPI(settings.AppID, settings.Account, settings.Password);
                streamingAPI.MarketCallback += (String json) =>
                {
                    Clients.All.Notify("Market", json);
                    Clients.All.MarketChanged(json);
                };

                streamingAPI.OrdersCallback += (String json) =>
                {
                    Clients.All.ordersChanged(json);
                    Debug.WriteLine("OrdersCallback");

                };
                streamingAPI.SubscribeOrders();
            }
            public override Task OnConnected()
            {
                Console.WriteLine(Context.ConnectionId + " connected from " + Context.Request.Url.Host);
                if (streamingAPI == null)
                {
                    ConnectStreamingAPI();
                }
                ConnectedIds.Add(Context.ConnectionId);
                return base.OnConnected();
            }
            public override Task OnReconnected()
            {
                Console.WriteLine(Context.ConnectionId + " reconnected from " + Context.Request.Url.Host);
                ConnectedIds.Add(Context.ConnectionId);
                return base.OnReconnected();
            }
            public override Task OnDisconnected(bool stopCalled)
            {
                try
                {
                    Console.WriteLine(Context.ConnectionId + " disconnected from " + Context.Request.Url.Host);
                }
                catch(Exception)
                {
                    Console.WriteLine(Context.ConnectionId + " disconnected");
                }
                ConnectedIds.Remove(Context.ConnectionId);
                if (ConnectedIds.Count == 0)
                {
                    streamingAPI = null;
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