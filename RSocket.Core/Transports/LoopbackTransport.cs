namespace RSocket.Transports;

using System.IO.Pipelines;

public class LoopbackTransport : IRSocketTransport
{
	IDuplexPipe Front, Back;
	public PipeReader Input => Front.Input;
	public PipeWriter Output => Front.Output;
	//public IRSocketServerTransport Server => this;

	public LoopbackTransport(PipeOptions? inputoptions = null, PipeOptions? outputoptions = null)
	{
		(Back, Front) = DuplexPipe.CreatePair(inputoptions, outputoptions);
	}

	//public Task ConnectAsync(CancellationToken cancel = default) => Task.CompletedTask;   //This is a noop because they are already connected.

	public ValueTask StartAsync(CancellationToken cancel = default) => ValueTask.CompletedTask;   //This is a noop because they are already connected.
	public ValueTask StopAsync() => ValueTask.CompletedTask;

	public void SendAsync(Payloads.FrameBuffer frame)
	{
	}

	public IRSocketTransport Beyond => new ServerTransport(this);       //TODO Maybe not Server? Backside? Otherside?

	struct ServerTransport : IRSocketTransport
	{
		IRSocketTransport Transport;
		public ServerTransport(IRSocketTransport transport) { Transport = transport; }
		public PipeReader Input => Transport.Input;
		public PipeWriter Output => Transport.Output;

		public void SendAsync(Payloads.FrameBuffer frame)
		{
		}
		public ValueTask StartAsync(CancellationToken cancel = default) => Transport.StartAsync(cancel);
		public ValueTask StopAsync() => Transport.StopAsync();
	}
}
