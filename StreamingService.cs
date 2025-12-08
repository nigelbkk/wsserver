using Microsoft.AspNet.SignalR;
using System;
using System.Threading;
using System.Threading.Tasks;
using WSServer;

public class StreamingService
{
	private BetfairStreamingAPIClient _client;
	private CancellationTokenSource _cts = new CancellationTokenSource();

	public void Start()
	{
		StartClient();
	}

	private void StartClient()
	{
		_client = new BetfairStreamingAPIClient();
		_client.OrdersCallback += OnOrdersReceived;
		_client.DisconnectedCallback += OnBetfairDisconnected;

		_ = _client.Connect();
	}

	private void OnOrdersReceived(string json1, string json2, string json3)
	{
		var hub = GlobalHost.ConnectionManager.GetHubContext<WebServicesHub>();
		hub.Clients.All.ordersChanged(json1, json2, json3);
	}

	private void OnBetfairDisconnected()
	{
		var hub = GlobalHost.ConnectionManager.GetHubContext<WebServicesHub>();
		hub.Clients.All.status("Betfair disconnected. Reconnecting...");

		// Start reconnect loop
		Task.Run(async () =>
		{
			while (!_cts.Token.IsCancellationRequested)
			{
				try
				{
					_client.Connect();
					hub.Clients.All.status("Betfair reconnected.");
					return;
				}
				catch
				{
					await Task.Delay(2000);
				}
			}
		});
	}
}
