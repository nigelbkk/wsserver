using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        static void Main(string[] args)
        {
			string url = "http://88.202.230.157:8088";
			url = "http://127.0.0.1:8088";
            url = "http://*:8088";

            SignalR = WebApp.Start<Startup>(url);
            Console.WriteLine("Waiting for connections on:  " + url);
            Console.ReadKey();
        }

        public class Startup
        {
            public void Configuration(IAppBuilder app)
            {
                Console.WriteLine("Startup.Configuration called!");
                HttpConfiguration config = new HttpConfiguration();

                config.MapHttpAttributeRoutes();
                config.Routes.MapHttpRoute( name: "DefaultApi", routeTemplate: "api/{controller}/{action}/{id}", defaults: new { id = RouteParameter.Optional } );

                // Simpler way to check controllers
                var controllerSelector = config.Services.GetService(typeof(System.Web.Http.Dispatcher.IHttpControllerSelector));
                Console.WriteLine($"Controller selector: {controllerSelector?.GetType().Name}");

                app.UseWebApi(config);
                Console.WriteLine("Web API configured");

                app.MapSignalR();
                Console.WriteLine("SignalR mapped");
            }
        }
        [HubName("WebSocketsHub")]
        public class MyHub : Hub
        {
            public static HashSet<string> ConnectedIds = new HashSet<string>();
            private static StreamingAPI streamingAPI = null;
            private void ConnectStreamingAPI()
            {
                Settings settings = Settings.DeSerialize();
                streamingAPI = new StreamingAPI(settings.AppID, settings.Account, settings.Password, settings.Cert, settings.CertPassword);
                streamingAPI.OrdersCallback += (String json1, String json2, String json3) =>
                {
                    //Debug.WriteLine("OrdersCallback");
                    Clients.All.ordersChanged(json1, json2, json3);
                };
                streamingAPI.SubscribeOrders();
            }
            public override Task OnConnected()
            {
                try
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
            // Add public static accessor
            public static StreamingAPI GetStreamingAPI()
            {
                return streamingAPI;
            }
            //public void SubscribeMarket(string marketid)
            //{
            //    Console.WriteLine(Context.ConnectionId + " subcribed to market " + marketid);
            //    streamingAPI.SubscribeMarket(marketid);
            //}
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

			//    Console.WriteLine(Context.ConnectionId + " subcribed to market " + marketid);
			Console.WriteLine($"subcribe to market {request.MarketId}");

			api.SubscribeMarket(request.MarketId);
            return Ok(new { subscribed = request.MarketId });
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
            Console.WriteLine("Status endpoint hit!");

            var api = Program.MyHub.GetStreamingAPI();

            if (api == null)
            {
                return BadRequest("Streaming API not connected");
            }
            api.SubscribeMarket("1.78587954");
            return Ok(new { status = "running", time = DateTime.Now });
        }

        // POST api/test/echo
        [HttpPost]
        public IHttpActionResult Echo([FromBody] EchoRequest req)
        {
            Console.WriteLine("Echo endpoint hit!");
            return Ok(new { message = $"echo received {req.Text}", time = DateTime.Now });
        }
    }
}