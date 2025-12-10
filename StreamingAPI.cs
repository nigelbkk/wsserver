using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Betfair.ESAClient;
using Betfair.ESAClient.Auth;
using Betfair.ESAClient.Cache;
using Betfair.ESASwagger.Model;
using System.Diagnostics;

namespace WSServer
{
	public delegate void StreamUpdateDelegate(string json1, string json2, string json3);
	class StreamingAPI
	{
		public StreamUpdateDelegate OrdersCallback = null;
		public StreamUpdateDelegate MarketCallback = null;
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
			Debug.WriteLine("StreamingAPI ctor");

			var savedListeners = Trace.Listeners.Cast<TraceListener>().ToList();
			Trace.Listeners.Clear();
			Debug.WriteLine("NewSessionProvider");
			NewSessionProvider("identitysso-cert.betfair.com", AppKey, BFUser, BFPassword, cert, cert_password);

			Debug.WriteLine("ClientCache.Start()");
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
			try
			{
				MarketCallback?.Invoke(JsonConvert.SerializeObject(e.Change), JsonConvert.SerializeObject(e.Market), JsonConvert.SerializeObject(e.Snap));
			}
			catch (Exception xe)
			{
				Debug.WriteLine(xe.Message);
			}
		}
		private void OnOrderChanged(object sender, OrderMarketChangedEventArgs e)
		{
			try
			{
				LastIncomingMessage = e;
				LastIncomingMessageTime = DateTime.UtcNow;
				OrdersCallback( JsonConvert.SerializeObject(e.Change), JsonConvert.SerializeObject(e.OrderMarket), JsonConvert.SerializeObject(e.Snap)); 
			}
			catch (Exception xe)
			{
				Debug.WriteLine($"{xe.Message}");
			}
		}
		public void SubscribeMarket(String marketID)
		{
			Debug.WriteLine("SubscribeMarket " + MarketId);
			MarketFilter f = new MarketFilter()
			{
				BettingTypes = new List<MarketFilter.BettingTypesEnum?>() { MarketFilter.BettingTypesEnum.Odds },
				MarketIds = new List<string>() { marketID }
			};

			MarketDataFilter mdf = new MarketDataFilter();
			mdf.LadderLevels = 3;
			mdf.Fields = new List<MarketDataFilter.FieldsEnum?>();
			mdf.Fields.Add(MarketDataFilter.FieldsEnum.ExBestOffers);
			mdf.Fields.Add(MarketDataFilter.FieldsEnum.ExTradedVol);
			mdf.Fields.Add(MarketDataFilter.FieldsEnum.ExLtp);
			mdf.Fields.Add(MarketDataFilter.FieldsEnum.ExMarketDef);

			MarketSubscriptionMessage msm = new MarketSubscriptionMessage()
			{
				MarketDataFilter = mdf,
				MarketFilter = f,
				SegmentationEnabled = true
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
