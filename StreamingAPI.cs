using Betfair.ESAClient;
using Betfair.ESAClient.Auth;
using Betfair.ESAClient.Cache;
using Betfair.ESASwagger.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Betfair.ESASwagger.Model.MarketDataFilter;

namespace WSServer
{
    public class MarketSnapDto
    {
        public bool InPlay { get; set; }
        public DateTime Time { get; set; }
        public List<MarketRunnerSnapDto> Runners { get; set; }
    }
    public class MarketRunnerSnapDto
    {
        public long SelectionId { get; set; }
        public MarketRunnerPricesDto Prices { get; set; }
    }
    public class MarketRunnerPricesDto
    {
        public List<PriceDto> Back { get; set; }
        public List<PriceDto> Lay { get; set; }
    }
    public class PriceDto
    {
        public double Price { get; set; }
        public double Size { get; set; }
    }




    public delegate void OrdersUpdateDelegate(string json1, string json2, string json3);
    public delegate void MarketUpdateDelegate(MarketChange mc, MarketSnapDto snap);
	class StreamingAPI
	{
		public OrdersUpdateDelegate OrdersCallback = null;
		public MarketUpdateDelegate MarketCallback = null;
		public DateTime LastIncomingMessageTime;
		public OrderMarketChangedEventArgs LastIncomingMessage;
		private static String ConnectionId { get; set; }
		private static String MarketId { get; set; }
		private static AppKeyAndSessionProvider SessionProvider { get; set; }
		private static ClientCache _clientCache;
		private static string _host = "stream-api.betfair.com";
		
		private static int _port = 443;

		public StreamingAPI(String AppKey, String BFUser, String BFPassword, string cert, string cert_password)
		{
			var savedListeners = Trace.Listeners.Cast<TraceListener>().ToList();
			Trace.Listeners.Clear();
			NewSessionProvider("identitysso-cert.betfair.com", AppKey, BFUser, BFPassword, cert, cert_password);

			ClientCache.Start();		// Connect WebSocket
			SubscribeOrders();         
			SubscribeMarket("1.251469597");

			ClientCache.Client.ConnectionStatusChanged += (o, e) =>
			{
				if (!String.IsNullOrEmpty(e.ConnectionId))
				{
					ConnectionId = e.ConnectionId;
				}
			};
			// Restore Trace listeners
			foreach (var listener in savedListeners)
			{
				Trace.Listeners.Add(listener);
			}
		}
		public void NewSessionProvider(string ssohost, string appkey, string username, string password, string cert, string cert_password)
		{
			AppKeyAndSessionProvider sessionProvider = new AppKeyAndSessionProvider(ssohost, appkey, username, password, cert, cert_password, "session_token");
			SessionProvider = sessionProvider;
		}
		public ClientCache ClientCache
		{
			get
			{
				if (_clientCache == null)
				{
					Client client = new Client(_host, _port, SessionProvider);
					_clientCache = new ClientCache(client);
					_clientCache.MarketCache.MarketChanged += OnMarketChanged;
					_clientCache.OrderCache.OrderMarketChanged += OnOrderChanged;

				}
				return _clientCache;
			}
		}
		private void OnMarketChanged(object sender, MarketChangedEventArgs e)
		{
			Debug.WriteLine("StreamingAPI OnMarketChanged");
			try
            {
                var runnerList = new List<MarketRunnerSnapDto>();

                foreach (var r in e.Snap.MarketRunners)
                {
                    var backList = new List<PriceDto>();
                    if (r.Prices.BestAvailableToBack != null)
                    {
                        foreach (var p in r.Prices.BestAvailableToBack)
                        {
                            backList.Add(new PriceDto { Price = p.Price, Size = p.Size });
                        }
                    }

                    var layList = new List<PriceDto>();
                    if (r.Prices.BestAvailableToLay != null)
                    {
                        foreach (var p in r.Prices.BestAvailableToLay)
                        {
                            layList.Add(new PriceDto { Price = p.Price, Size = p.Size });
                        }
                    }

                    var dto = new MarketRunnerSnapDto
                    {
                        SelectionId = r.RunnerId.SelectionId,
                        Prices = new MarketRunnerPricesDto
                        {
                            Back = backList,
                            Lay = layList
                        }
                    };

                    runnerList.Add(dto);
                }

				MarketSnapDto snap = new MarketSnapDto()
				{
                    InPlay = e.Snap.MarketDefinition?.InPlay ?? false,
                    Time = e.Snap.Time,
                    Runners = runnerList
				};

                MarketCallback?.Invoke(e.Change, snap);
			}
			catch (Exception xe)
			{
				Debug.WriteLine(xe.Message);
			}
		}
		private void OnOrderChanged(object sender, OrderMarketChangedEventArgs e)
		{
			//Debug.WriteLine("StreamingAPI OnOrderChanged");
			try
			{
				LastIncomingMessage = e;
				LastIncomingMessageTime = DateTime.UtcNow;
				//OrdersCallback(JsonConvert.SerializeObject(e.Change), JsonConvert.SerializeObject(e.OrderMarket), JsonConvert.SerializeObject(e.Snap));
				OrdersCallback( JsonConvert.SerializeObject(e.Change), null, JsonConvert.SerializeObject(e.Snap)); 
			}
			catch (Exception xe)
			{
				Debug.WriteLine($"{xe.Message}");
			}
		}
		public void SubscribeMarket(String marketId)
		{
			Debug.WriteLine("SubscribeMarket " + MarketId);
            MarketSubscriptionMessage msm = new MarketSubscriptionMessage
            {
                Op = "marketSubscription",
                Id = 1,
                MarketFilter = new MarketFilter
                {
                    MarketIds = new List<string> { marketId }  // your string variable
                },
                MarketDataFilter = new MarketDataFilter
                {
                    Fields = new List<FieldsEnum?> { FieldsEnum.ExBestOffers, FieldsEnum.ExLtp, FieldsEnum.ExMarketDef, FieldsEnum.ExTraded },
                    LadderLevels = 3
                }
            };

            ClientCache.SubscribeMarkets(msm);
		}
		public void SubscribeOrders()
		{ 
			OrderSubscriptionMessage osm = new OrderSubscriptionMessage()
			{
				SegmentationEnabled = true
			};
			ClientCache.SubscribeOrders(osm);
		}
		public void Stop()
		{
			ClientCache.Stop();
		}
	}
}
