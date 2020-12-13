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

[assembly: OwinStartup(typeof(Program.Startup))]
namespace WSServer
{
    class Program
    {
        public OrdersUpdateDelegate OrdersEventSink = null;
        static IDisposable SignalR;

        static void Main(string[] args)
        {
            string url = "http://88.202.183.202:8088";
            url = "http://192.168.1.6:8088";
            SignalR = WebApp.Start(url);

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
            public static ConcurrentDictionary<string, string> Connections = new ConcurrentDictionary<string, string>();
            private StreamingAPI streamingAPI = null;
            static int idx = 0;
            private static System.Timers.Timer timer = null;
            public MyHub()
            {
                if (streamingAPI == null)
                {
                    Settings settings = Settings.DeSerialize();
                    streamingAPI = new StreamingAPI(settings.AppID, settings.Account, settings.Password);
                    StreamingAPI.Callback += (String json) =>
                    {
                        Clients.All.MarketChanged(json);
                    };

                    StreamingAPI.Callback += (String json) =>
                    {
                        Clients.All.ordersChanged(json);
                    };
                    timer = new System.Timers.Timer(1000) { Enabled = true, };
                    timer.Elapsed += (s, a) =>
                    {
                        //Clients.All.addMessage(idx++, DateTime.Now.ToLongTimeString());
                    };
                }
            }
            public override Task OnConnected()
            {
                Console.WriteLine(Context.ConnectionId + " connected from " + Context.Request.Url.Host);
                return base.OnConnected();
            }
            public override Task OnDisconnected(bool stopCalled)
            {
                Console.WriteLine(Context.ConnectionId + " disconnected from " + Context.Request.Url.Host);
                return base.OnDisconnected(stopCalled);
            }
            public void Send(string message)
            {
                Clients.All.addMessage(message, "Awooga");
            }
            public void SubscribeMarket(string marketid)
            {
                Console.WriteLine(Context.ConnectionId + " subcribed to market " + marketid);
                streamingAPI.SubscribeMarket(marketid);
                Clients.Client(Context.ConnectionId).Message("You just subscribed to market " + marketid);
            }
            public void SubscribeOrders()
            {
                streamingAPI.SubscribeOrders();
                Clients.All.Message("SubscribeOrders");
            }
            public void UnsubscribeOrders()
            {
                Clients.All.Message("message, ");
            }
        }
    }
}