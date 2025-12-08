using Microsoft.Owin.Hosting;
using System;

public static class BetfairStreamHost
{
	private static IDisposable _signalRHost;
	private static StreamingService _streamingService;

	public static void Start()
	{
		// 1. Start SignalR server
		string url = "http://localhost:8080/";
		_signalRHost = WebApp.Start<Startup>(url);
		Console.WriteLine("[BetfairStreamHost] SignalR started on " + url);

		// 2. Start Betfair streaming service
		_streamingService = new StreamingService();
		_streamingService.Start();

		Console.WriteLine("[BetfairStreamHost] StreamingService started.");
	}
}
