using Microsoft.AspNet.SignalR;

public class WebServicesHub : Hub
{
	public void SendStatus(string message)
	{
		Clients.All.status(message);
	}
}
