
/*
 * using System;
using System.Threading.Tasks;

public class BetfairStreamingAPIClient
{
	public event Action<string, string, string> OrdersCallback;
	public event Action Disconnected;

	// Your existing fields and constructor

	public async Task Connect()
	{
		// your existing Betfair login + websocket connect logic
		// THEN:
		// start listening loop and call HandleDisconnect() if needed
	}

	private void HandleDisconnect()
	{
		Disconnected?.Invoke();
	}

	// from your existing streaming logic:
	private void RaiseOrdersChanged(string j1, string j2, string j3)
	{
		OrdersCallback?.Invoke(j1, j2, j3);
	}
}
*/

using System;
using System.Collections.Generic;
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
	class BetfairStreamingAPIClient
	{
		public event Action<string, string, string> OrdersCallback;
		//public event Action Disconnected;
		public event Action DisconnectedCallback;

		//public StreamUpdateDelegate OrdersCallback = null;
		public StreamUpdateDelegate MarketCallback = null;
		private static String ConnectionId { get; set; }
		private static String MarketId { get; set; }
		private static AppKeyAndSessionProvider SessionProvider { get; set; }
		private static ClientCache _clientCache;
		private static string _host = "stream-api.betfair.com";
		//		private static string _host = "stream-api-integration.betfair.com";

		private static int _port = 443;

		public BetfairStreamingAPIClient()
		{

		}
		public BetfairStreamingAPIClient(String AppKey, String BFUser, String BFPassword, string cert, string cert_password)
		{
			NewSessionProvider("identitysso-cert.betfair.com", AppKey, BFUser, BFPassword, cert, cert_password);
			ClientCache.Client.ConnectionStatusChanged += (o, e) =>
			{
				Console.WriteLine($"ConnectionStatusChanged from {e.Old} to {e.New}");
				Console.WriteLine($"ConnectionStatusChanged from {e.Old} to {e.New}");
				if (!String.IsNullOrEmpty(e.ConnectionId))
				{
					ConnectionId = e.ConnectionId;
				}
			};
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

		public async Task Connect()
		{
			// your existing Betfair login + websocket connect logic
			// THEN:
			// start listening loop and call HandleDisconnect() if needed

			Settings settings = Settings.DeSerialize();
			NewSessionProvider("identitysso-cert.betfair.com", settings.AppID, settings.Account, settings.Password, settings.Cert, settings.CertPassword);
			ClientCache.Client.ConnectionStatusChanged += (o, e) =>
			{
				Console.WriteLine($"ConnectionStatusChanged from {e.Old} to {e.New}");
				if (!String.IsNullOrEmpty(e.ConnectionId))
				{
					ConnectionId = e.ConnectionId;
				}
			};
		}
		private void HandleDisconnect()
		{
			DisconnectedCallback?.Invoke();
		}
		private void RaiseOrdersChanged(string j1, string j2, string j3)
		{
			OrdersCallback?.Invoke(j1, j2, j3);
		}
		private void OnMarketChanged(object sender, MarketChangedEventArgs e)
		{
			try
			{
				MarketCallback?.Invoke(e.Change.ToJson(), "", "");
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
				OrdersCallback(JsonConvert.SerializeObject(e.Change), JsonConvert.SerializeObject(e.OrderMarket), JsonConvert.SerializeObject(e.Snap));
			}
			catch (Exception xe)
			{
				Debug.WriteLine($"{xe.Message}");
			}
		}
		public void SubscribeMarket(String marketID)
		{
			Console.WriteLine("Start " + MarketId);
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
