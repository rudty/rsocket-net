namespace RSocket;

using System.IO.Pipelines;

/// <summary>
/// A Pipeline Transport for a connectable RSocket. Once connected, the Input and Output Pipelines can be used to communicate abstractly with the RSocket bytestream.
/// </summary>
public interface IRSocketTransport
{
	PipeReader Input { get; }
	PipeWriter Output { get; }

	ValueTask StartAsync(CancellationToken cancel = default);
	ValueTask StopAsync();
}

/// <summary>
/// A Pipeline Transport for a serving RSocket.
/// </summary>
public interface IRSocketServerTransport
{
	PipeReader Input { get; }
	PipeWriter Output { get; }

	Task StartAsync(CancellationToken cancel = default);
	Task StopAsync();
}
