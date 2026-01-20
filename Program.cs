using Betfair.ESAClient;
using Betfair.ESAClient.Cache;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.Remoting.Contexts;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using WSServer;

[assembly: OwinStartup(typeof(Program.Startup))]
namespace WSServer
{
	class Program
    {
        static IDisposable SignalR;
        static StreamingAPI streamingAPI;
		static void Main(string[] args)
        {
			string url = "http://88.202.230.157:8088";
			url = "http://127.0.0.1:8088";
            url = "http://*:8088";

            SignalR = WebApp.Start<Startup>(url);

			Settings settings = Settings.DeSerialize();
			streamingAPI = new StreamingAPI(settings.AppID, settings.Account, settings.Password, settings.Cert, settings.CertPassword);

			Debug.WriteLine("Waiting for connections on:  " + url);
            Console.ReadKey();
        }

        public class Startup
        {
            public void Configuration(IAppBuilder app)
            {
                HttpConfiguration config = new HttpConfiguration();
				config.MapHttpAttributeRoutes();
                config.Routes.MapHttpRoute( name: "DefaultApi", routeTemplate: "api/{controller}/{action}/{id}", defaults: new { id = RouteParameter.Optional } );
				app.UseWebApi(config);
				app.MapSignalR();
			}
		}
        [HubName("WebSocketsHub")]
        public class WebSocketsHub : Hub
        {
			class ClientConnection
			{
				public long Id { get; }
				public WebSocket Socket { get; }

				public ClientConnection(long id, WebSocket socket)
				{
					Id = id;
					Socket = socket;
				}
			}
			public static HashSet<string> ConnectedIds = new HashSet<string>();
			private static ConcurrentDictionary<string, HashSet<string>> _subscriptions = new ConcurrentDictionary<string, HashSet<string>>();
			private static StreamingAPI streamingAPI;
			private static String _keepAliveMarket;

			public WebSocketsHub()
            {
				ConnectStreamingAPI();
			}

			public static void SubscribeMarket(String connectionId, String marketId)
			{
				lock (_subscriptions)
				{
					if (!_subscriptions.TryGetValue(marketId, out var set))
					{
						set = new HashSet<string>();
						_subscriptions[marketId] = set;

						// first subscriber → subscribe upstream
						if (streamingAPI != null)
							streamingAPI.SubscribeMarket(marketId);
						_keepAliveMarket = marketId;
					}

					set.Add(connectionId);
				}
			}
			public static void UnSubscribeMarket(String connectionId, String marketId)
			{
				if (!_subscriptions.TryGetValue(marketId, out var set))
					return;

				set.Remove(connectionId);

				if (set.Count == 0)
				{
					// IMPORTANT: only unsubscribe if this is NOT your "keep-alive" market
					if (marketId != _keepAliveMarket)
					{
                        if (streamingAPI != null)
                            streamingAPI.UnSubscribeMarket(marketId);
						_subscriptions.TryRemove(marketId, out set);
					}
					else
					{
                        Debug.WriteLine($"{marketId} kept alive");
                    }
                }
			}

			private void ConnectStreamingAPI()
            {
				Settings settings = Settings.DeSerialize();
                streamingAPI = Program.streamingAPI;
				streamingAPI.OrdersCallback = (String json1, String json2, String json3) =>				
				{
                    //Debug.WriteLine("Hub OrdersCallback");
                    try
                    {
                        Clients.All.ordersChanged(json1, json2, json3);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
				};
				streamingAPI.MarketCallback += (MarketChangeDto change) =>
				{
					//Debug.WriteLine($"Hub MarktCallback:");
                    try
                    {
				    	Clients.All.marketChanged(change);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                };
			}
			public override Task OnConnected()
            {
				Debug.WriteLine("Hub OnConnected");
				try
				{
                    object ipAddress;

                    if (streamingAPI == null)
                    {
                        //ConnectStreamingAPI();
                    }
                    Context.Request.Environment.TryGetValue("server.RemoteIpAddress", out ipAddress);
					Debug.WriteLine(DateTime.UtcNow.ToString("HH:mm:ss") + " " + ipAddress + " connected");
                    ConnectedIds.Add(Context.ConnectionId);
					return base.OnConnected();
                }
				catch (Exception ex)
				{
					Debug.WriteLine($"OnConnected ERROR: {ex.Message}");
					//Debug.WriteLine($"Stack: {ex.StackTrace}");
					throw; // Re-throw so you see it in logs
				}
			}
            public override Task OnReconnected()
            {
                object ipAddress;
                Context.Request.Environment.TryGetValue("server.RemoteIpAddress", out ipAddress);
				Debug.WriteLine(DateTime.UtcNow.ToString("HH:mm:ss") + " " + ipAddress + " reconnected");
                ConnectedIds.Add(Context.ConnectionId);
				return base.OnReconnected();
            }
            public override Task OnDisconnected(bool stopCalled)
            {
                try
                {
                    object ipAddress;
                    Context.Request.Environment.TryGetValue("server.RemoteIpAddress", out ipAddress);
					Debug.WriteLine(DateTime.UtcNow.ToString("HH:mm:ss") + " " + ipAddress + " disconnected");
				}
				catch (Exception)
                {
					Debug.WriteLine(Context.ConnectionId + " disconnected");
                }
                ConnectedIds.Remove(Context.ConnectionId);
                return base.OnDisconnected(stopCalled);
            }
			// Add public static accessor
			public static StreamingAPI GetStreamingAPI()
			{
				return streamingAPI;
			}
		}
	}

    [RoutePrefix("api/market")]
    public class MarketController : ApiController
    {
		public class MarketSubscribeRequest
        {
            public string MarketId { get; set; }
        }

        [HttpPost]
        [Route("subscribe")]
		public IHttpActionResult SubscribeMarket([FromBody] MarketSubscribeRequest request)
		{
			Debug.WriteLine($"{this.Url.Request.RequestUri.IdnHost} subcribes to market {request.MarketId}");
			Program.WebSocketsHub.SubscribeMarket(this.Url.Request.RequestUri.IdnHost, request.MarketId);

			return Ok(new { subscribed = request.MarketId });
		}
        [HttpPost]
        [Route("unsubscribe")]
        public IHttpActionResult UnSubscribeMarket([FromBody] MarketSubscribeRequest request)
		{
			Debug.WriteLine($"{this.Url.Request.RequestUri.IdnHost} unsubcribes from market {request.MarketId}");
			Program.WebSocketsHub.UnSubscribeMarket(this.Url.Request.RequestUri.IdnHost, request.MarketId);

			return Ok(new { unsubscribed = request.MarketId });
		}
    }
}