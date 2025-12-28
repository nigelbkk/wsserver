using Betfair.ESAClient;
using Betfair.ESAClient.Auth;
using Betfair.ESAClient.Cache;
using Betfair.ESASwagger.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Betfair.ESASwagger.Model.MarketDataFilter;

namespace WSServer
{
    public class MarketChangeDto
    {
        public string MarketId { get; set; }
        public DateTime Time { get; set; }
		public MarketDefinition.StatusEnum? Status { get; set; }
		public List<RunnerChangeDto> Runners { get; set; }
    }

    public class RunnerChangeDto
    {
        public long Id { get; set; }
        public double? Ltp { get; set; }
        public double? Tv { get; set; }

        public List<List<double?>> Trd { get; set; }
        public List<PriceLevelDto> Bdatb { get; set; }
        public List<PriceLevelDto> Bdatl { get; set; }
    }

    public class PriceLevelDto
    {
        public int Level { get; set; }
        public double Price { get; set; }
        public double Size { get; set; }
    }
    public class MarketSnapDto
    {
        public String MarketId { get; set; }
        public bool InPlay { get; set; }
        public DateTime Time { get; set; }
		public MarketDefinition.StatusEnum? Status { get; set; }
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
    //public delegate void MarketUpdateDelegate(MarketSnapDto snap);
    public delegate void MarketUpdateDelegate(MarketChangeDto change);
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
        public static MarketChangeDto BuildMarketChangeDto(DateTime time, MarketDefinition.StatusEnum? status, string marketId, MarketChange change) 
        {
            var dto = new MarketChangeDto
            {
                MarketId = marketId,
                Runners = new List<RunnerChangeDto>(),
                Time = time,
                Status = status
            };

            var json = JObject.Parse(change.ToJson());
            var rcArray = json["rc"] as JArray;

            if (rcArray == null)
                return dto;

            foreach (var rc in rcArray)
            {
                var runner = new RunnerChangeDto
                {
                    Id = rc["id"] != null ? rc["id"].Value<long>() : 0,
                    Ltp = rc["ltp"]?.Value<double?>(),
                    Tv = rc["tv"]?.Value<double?>(),
                    Bdatb = new List<PriceLevelDto>(),
                    Bdatl = new List<PriceLevelDto>(),
                    Trd = new List<List<double?>>(),
                };

                var tradeArray = rc["trd"] as JArray;
                if (tradeArray != null)
                {
                    foreach (var trade in tradeArray)
                    {
                        double? price = trade[0].ToObject<double?>();
                        double? volume = trade[1].ToObject<double?>();

                        runner.Trd.Add(new List<double?> { price, volume });
                    }
                }

                var bdatbArray = rc["bdatb"] as JArray;
                if (bdatbArray != null)
                {
                    foreach (var level in bdatbArray)
                    {
                        runner.Bdatb.Add(new PriceLevelDto
                        {
                            Level = level[0].Value<int>(),
                            Price = level[1].Value<double>(),
                            Size = level[2].Value<double>()
                        });
                    }
                }

                var bdatlArray = rc["bdatl"] as JArray;
                if (bdatlArray != null)
                {
                    foreach (var level in bdatlArray)
                    {
                        runner.Bdatl.Add(new PriceLevelDto
                        {
                            Level = level[0].Value<int>(),
                            Price = level[1].Value<double>(),
                            Size = level[2].Value<double>()
                        });
                    }
                }

                dto.Runners.Add(runner);
            }

            return dto;
        }

        private void OnMarketChanged(object sender, MarketChangedEventArgs e)
        {
            String json = e.Change.ToJson();
            DateTime Time = e.Snap.Time;

			MarketChangeDto changeDto = BuildMarketChangeDto(e.Snap.Time, e.Snap.MarketDefinition.Status, e.Change.Id, e.Change);

            MarketCallback?.Invoke(changeDto);
        }
		private void OnOrderChanged(object sender, OrderMarketChangedEventArgs e)
		{
            //Debug.WriteLine("StreamingAPI OnOrderChanged");
            try
            {
				LastIncomingMessage = e;
				LastIncomingMessageTime = DateTime.UtcNow;
				if (OrdersCallback != null)
					OrdersCallback( JsonConvert.SerializeObject(e.Change), null, JsonConvert.SerializeObject(e.Snap)); 
			}
			catch (Exception xe)
			{
				Debug.WriteLine($"{xe.Message}");
			}
		}
		int? subscription_count = 0;
		private HashSet<String> _subscriptions = new HashSet<string>();
		public void SubscribeMarket(String marketId)
		{
			Debug.WriteLine("SubscribeMarket " + MarketId);
            _subscriptions.Add(marketId);

            MarketSubscriptionMessage msm = new MarketSubscriptionMessage
			{
				Op = "marketSubscription",
				Id = subscription_count++,

				MarketFilter = new MarketFilter
				{
					MarketIds = _subscriptions.ToList()
				},
				MarketDataFilter = new MarketDataFilter
				{
					Fields = new List<FieldsEnum?> { FieldsEnum.ExBestOffersDisp, FieldsEnum.ExTraded, FieldsEnum.ExTradedVol, FieldsEnum.ExBestOffers, FieldsEnum.ExLtp, FieldsEnum.ExMarketDef },
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
