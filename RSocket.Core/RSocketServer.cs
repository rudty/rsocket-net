namespace RSocket;

public class RSocketServer : RSocket1
{
	public RSocketServer(IRSocketTransport transport, PrefetchOptions? options = null) : base(transport, options) { }

	public async Task ConnectAsync()
	{
		await Transport.StartAsync();
		_ = Connect(CancellationToken.None);
	}

	public override void Setup(in RSocketProtocol.Setup value)
	{

	}
}

