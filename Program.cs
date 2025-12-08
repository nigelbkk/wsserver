using Microsoft.Owin.Hosting;
using System;

public class Program
{
	public static StreamingService StreamingServiceInstance;

	public static void Main(string[] args)
	{
		string url = "http://localhost:8080";

		// Start SignalR host
		using (WebApp.Start<Startup>(url))
		{
			Console.WriteLine("SignalR host running at " + url);

			// Start the streaming service once, globally
			StreamingServiceInstance = new StreamingService();
			StreamingServiceInstance.Start();

			Console.WriteLine("Press ENTER to quit.");
			Console.ReadLine();
		}
	}
}
