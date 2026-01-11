namespace RSocket;

using RSocket.Frame;
using System.Buffers;

public class RSocketClient : RSocket1
{
	Task Handler;
	RSocketOptions Options { get; set; }

	public RSocketClient(IRSocketTransport transport, RSocketOptions? options = default) : base(transport, options) { }

	public async Task ConnectAsync(RSocketOptions options, Memory<byte> metadata, Memory<byte> data)
	{
		await Transport.StartAsync();
		Handler = Connect(CancellationToken.None);
		options ??= RSocketOptions.Default;
		Setup2(options.KeepAlive, options.Lifetime, options.MetadataMimeType, options.DataMimeType, data: data, metadata: metadata);
	}

	public void Setup2(TimeSpan keepalive, TimeSpan lifetime, string? metadataMimeType = null, string? dataMimeType = null, Memory<byte> data = default, Memory<byte> metadata = default)
	{
		var setup = new SetupCodec(
			keepalive: (int)keepalive.TotalMilliseconds,
			lifetime: (int)lifetime.TotalMilliseconds,
			metadataMimeType: metadataMimeType ?? string.Empty,
			dataMimeType: dataMimeType ?? string.Empty,
			resumeToken: null,
			dataLength: data.Length,
			metadataLength: metadata.Length);

		var frameBuffer = FrameBuffer.Create(setup.Length);
		setup.Encode(frameBuffer, data: data, metadata: metadata);
		Transport.SendAsync(frameBuffer);
	}

	/// <summary>A simplfied RSocket Client that operates only on UTF-8 strings.</summary>
	//public class ForStrings
	//{
	//	private readonly RSocketClient Client;
	//	public ForStrings(RSocketClient client) { Client = client; }
	//	public Task<string> RequestResponse(string data, string metadata = default) => Client.RequestResponse(value => Encoding.UTF8.GetString(value.data.ToArray()), new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)), metadata == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)));
	//	public IAsyncEnumerable<string> RequestStream(string data, string metadata = default)
	//	{
	//		return Client.RequestStream(value =>
	//		{
	//			return Encoding.UTF8.GetString(value.data.ToArray());
	//		}, new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)), metadata == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)));
	//	}

	//	public IAsyncEnumerable<string> RequestChannel(IAsyncEnumerable<string> inputs, string data = default, string metadata = default) =>
	//		Client.RequestChannel(inputs, input => new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(input)), result => Encoding.UTF8.GetString(result.data.ToArray()),
	//			data == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)),
	//			metadata == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)));
	//}
}
