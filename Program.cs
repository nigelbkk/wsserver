using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Remoting.Contexts;
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
        public class MyHub : Hub
        {
            public static HashSet<string> ConnectedIds = new HashSet<string>();
			public static Tuple<String, DateTime> LastConnection;
			public static Tuple<String, DateTime> LastReConnection;
			public static Tuple<String, DateTime> LastDisConnection;
            private static StreamingAPI streamingAPI;

            public MyHub()
            {
				ConnectStreamingAPI();
			}
            private void ConnectStreamingAPI()
            {
				Settings settings = Settings.DeSerialize();
                streamingAPI = Program.streamingAPI;
				streamingAPI.OrdersCallback = (String json1, String json2, String json3) =>				
				{
					//Debug.WriteLine("Hub OrdersCallback");
					Clients.All.ordersChanged(json1, json2, json3);
				};
				streamingAPI.MarketCallback += (String json1, String json2, String json3) =>
				{
					//Debug.WriteLine($"Hub MarktCallback: {json1}");
					Clients.All.marketChanged(json1, json2, json3);
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
					LastConnection=new Tuple<string, DateTime> (Context.ConnectionId, DateTime.UtcNow );

					return base.OnConnected();
                }
				catch (Exception ex)
				{
					Debug.WriteLine($"OnConnected ERROR: {ex.Message}");
					Debug.WriteLine($"Stack: {ex.StackTrace}");
					throw; // Re-throw so you see it in logs
				}
			}
            public override Task OnReconnected()
            {
                object ipAddress;
                Context.Request.Environment.TryGetValue("server.RemoteIpAddress", out ipAddress);
				Debug.WriteLine(DateTime.UtcNow.ToString("HH:mm:ss") + " " + ipAddress + " reconnected");
                ConnectedIds.Add(Context.ConnectionId);
				LastReConnection = new Tuple<string, DateTime>(Context.ConnectionId, DateTime.UtcNow);
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
				LastDisConnection = new Tuple<string, DateTime>(Context.ConnectionId, DateTime.UtcNow);
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
            var api = Program.MyHub.GetStreamingAPI();

            if (api == null)
            {
                return BadRequest("Streaming API not connected");
            }
            Debug.WriteLine($"subcribe to market {request.MarketId}");
			api.SubscribeMarket(request.MarketId);
            return Ok(new { subscribed = request.MarketId });
        }
		[HttpGet]
		public IHttpActionResult Capture()
		{
			Debug.WriteLine("Capture endpoint hit", DateTime.UtcNow);
			var api = Program.MyHub.GetStreamingAPI();

			if (api == null)
			{
				return BadRequest("Streaming API not connected");
			}
			return Ok(new { 
                status = "running", 
                time = DateTime.UtcNow,
				LastIncomingMessageTime = api.LastIncomingMessageTime,
				LastConnection = Program.MyHub.LastConnection,
				LastDisConnection = Program.MyHub.LastDisConnection,
				LastReConnection = Program.MyHub.LastReConnection,
                Program.MyHub.ConnectedIds,
				LastIncomingMessage = api.LastIncomingMessage
            });
		}
	}

	public class TestController : ApiController
    {
        public class EchoRequest
        {
            public string Text { get; set; }
            public int Count { get; set; }
        }


		// GET api/test/status
		[HttpGet]
		public IHttpActionResult Status()
		{
			Debug.WriteLine("Status endpoint hit", DateTime.UtcNow);
			var api = Program.MyHub.GetStreamingAPI();

			if (api == null)
			{
				return BadRequest("Streaming API not connected");
			}
			return Ok(new { status = "running", time = DateTime.Now, } );
		}

		[HttpPost]
		public IHttpActionResult Stop()
		{
			Debug.WriteLine("Stop endpoint hit!");

			var api = Program.MyHub.GetStreamingAPI();

			if (api == null)
			{
				return BadRequest("Streaming API not connected");
			}
            api.Stop();
			return Ok("Server stopped");
		}

		// POST api/test/echo
		[HttpPost]
        public IHttpActionResult Echo([FromBody] EchoRequest req)
        {
			Debug.WriteLine("Echo endpoint hit!");
            return Ok(new { message = $"echo received {req.Text}", time = DateTime.Now });
        }
    }
}