namespace RSocket;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using IRSocketStream = System.IObserver<DataAndMetadata>;

public readonly struct DataAndMetadata(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)
{
	// TODO RENAME Metadata, Data
	public ReadOnlySequence<byte> metadata { get; } = metadata;
	public ReadOnlySequence<byte> data { get; } = data;
}

public readonly ref struct RefDataAndMetadata(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)
{
	// TODO RENAME Metadata, Data
	public ReadOnlySequence<byte> metadata { get; } = metadata;
	public ReadOnlySequence<byte> data { get; } = data;

	// TODO REMOVE
	public static implicit operator RefDataAndMetadata((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) tempCode)
	{
		return new RefDataAndMetadata(tempCode.metadata, tempCode.data);
	}
}

public interface IRSocketChannel
{
	Task Send(RefDataAndMetadata value);
}

public partial class RSocket : IRSocketProtocol
{
	PrefetchOptions Options { get; set; }
	private CancellationToken cancellationToken;

	//TODO Hide.
	public IRSocketTransport Transport { get; set; }

	private int _streamId = 1 - 2;       //SPEC: Stream IDs on the client MUST start at 1 and increment by 2 sequentially, such as 1, 3, 5, 7, etc
	private int NewStreamId() => Interlocked.Add(ref _streamId, 2);  //TODO SPEC: To reuse or not... Should tear down the client if this happens or have to skip in-use IDs.

	private readonly ConcurrentDictionary<int, IRSocketStream> Dispatcher = new ConcurrentDictionary<int, IRSocketStream>();

	private int StreamDispatch(IRSocketStream transform)
	{
		var id = NewStreamId();
		 Dispatcher[id] = transform; return id;
	}
	//TODO Stream Destruction - i.e. removal from the dispatcher.

	public RSocket(IRSocketTransport transport, PrefetchOptions options = default)
	{
		Transport = transport;
		Options = options ?? PrefetchOptions.Default;
	}

	/// <summary>Binds the RSocket to its Transport and begins handling messages.</summary>
	/// <param name="cancel">Cancellation for the handler. Requesting cancellation will stop message handling.</param>
	/// <returns>The handler task.</returns>
	public Task Connect(CancellationToken cancel = default) => RSocketProtocol.Handler(this, Transport.Input, cancel);
	public Task Setup(TimeSpan keepalive, TimeSpan lifetime, string metadataMimeType = null, string dataMimeType = null, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default) => new RSocketProtocol.Setup(keepalive, lifetime, metadataMimeType: metadataMimeType, dataMimeType: dataMimeType, data: data, metadata: metadata).WriteFlush(Transport.Output, data: data, metadata: metadata);

	//TODO SPEC: A requester MUST not send PAYLOAD frames after the REQUEST_CHANNEL frame until the responder sends a REQUEST_N frame granting credits for number of PAYLOADs able to be sent.
	public async ValueTask RequestChannel(
		IRSocketStream stream,
		IAsyncEnumerable<DataAndMetadata> clientInputEnumerable,
		int initial = RSocketOptions.INITIALDEFAULT)
	{
		var id = StreamDispatch(stream);
		var clientInputEnumerableWithCancellation = clientInputEnumerable.WithCancellation(cancellationToken);

		await foreach (var dataAndMetadata in clientInputEnumerableWithCancellation)
		{
			var requestChannel = new RSocketProtocol.RequestChannel(
				id,
				dataAndMetadata.data,
				dataAndMetadata.metadata,
				initialRequest: Options.GetInitialRequestSize(initial));
			await requestChannel.WriteFlush(Transport.Output, dataAndMetadata.data, dataAndMetadata.metadata);
		}


	}

	// protected class ChannelHandler : IRSocketChannel       //TODO hmmm...
	// {
	// 	readonly RSocket Socket;
	// 	readonly int Stream;
	//
	// 	public ChannelHandler(RSocket socket, int stream) { Socket = socket; Stream = stream; }
	//
	// 	public Task Send(RefDataAndMetadata value)
	// 	{
	// 		if (!Socket.Dispatcher.ContainsKey(Stream)) { throw new InvalidOperationException("Channel is closed"); }
	// 		return new RSocketProtocol.Payload(Stream, value.data, value.metadata, next: true).WriteFlush(Socket.Transport.Output, value.data, value.metadata);
	// 	}
	// }

	public async ValueTask RequestStream(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
	{
		var id = StreamDispatch(stream);
		await new RSocketProtocol.RequestStream(id, data, metadata, initialRequest: Options.GetInitialRequestSize(initial)).WriteFlush(Transport.Output, data, metadata);
	}

	public async ValueTask RequestResponse(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
	{
		var id = StreamDispatch(stream);
		await new RSocketProtocol.RequestResponse(id, data, metadata).WriteFlush(Transport.Output, data, metadata);
	}

	public void RequestFireAndForget(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
	{
		var id = StreamDispatch(stream);
		new RSocketProtocol.RequestFireAndForget(id, data, metadata).WriteFlush(Transport.Output, data, metadata);
	}

	public void Payload(in RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
	{
		//Console.WriteLine($"{value.Header.Stream:0000}===>{Encoding.UTF8.GetString(value.Data.ToArray())}");
		if (Dispatcher.TryGetValue(message.Stream, out var transform))
		{
			if (message.IsNext)
			{
				transform.OnNext(new DataAndMetadata(metadata, data));
			}

			if (message.IsComplete)
			{
				transform.OnCompleted();
			}
		}
		else
		{
			//TODO Log missing stream here.
		}
	}

	public virtual void Setup(in RSocketProtocol.Setup value) => throw new InvalidOperationException($"Client cannot process Setup frames");    //TODO This exception just stalls processing. Need to make sure it's handled.

	void IRSocketProtocol.Error(in RSocketProtocol.Error message)
	{
		if (Dispatcher.TryGetValue(message.Stream, out var transform))
		{
			transform.OnError(new RSocketException(message.ErrorText, message.ErrorCode));
		}
		else
		{
			//TODO Log missing stream here.
		}
	}

	void IRSocketProtocol.RequestFireAndForget(in RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) => throw new NotImplementedException();

	void IRSocketProtocol.RequestResponse(
		in RSocketProtocol.RequestResponse message,
		ReadOnlySequence<byte> metadata,
		ReadOnlySequence<byte> data)
	{
		var streamId = message.Stream;

		try
		{
			var payload = new RSocketProtocol.Payload(
				streamId,
				data,
				metadata,
				next: true,
				complete: true);

			payload.WriteFlush(Transport.Output, data, metadata);
		}
		catch (Exception ex)
		{
			// TODO.LOG
		}
	}

	void IRSocketProtocol.RequestStream(
		in RSocketProtocol.RequestStream message,
		ReadOnlySequence<byte> metadata,
		ReadOnlySequence<byte> data)
	{
		var streamId = message.Stream;
		// TODO. NEXT 를 HEADER 로 옮김.
		var payload = new RSocketProtocol.Payload(streamId, next: true);
		Payload(payload, metadata, data);
	}


	//TODO, probably need to have an IAE<T> pipeline overload too.

	void IRSocketProtocol.RequestChannel(in RSocketProtocol.RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
	{
		var streamId = message.Stream;
		var payload = new RSocketProtocol.Payload(streamId, next: true);
		Payload(payload, metadata, data);
		// var outgoing = Observable.Create<DataAndMetadata>((d) =>
		// {
		// 	return () => StreamDispatch(streamId, d);
		// });
		//
		// _ = ReceiveStream(streamId, outgoing);
	}

	// private async Task ReceiveStream(int streamId, DataAndMetadata source)
	// {
	// 	foreach (var item in source)
	// 	{
	// 		var payload = new RSocketProtocol.Payload(streamId, item.data, item.metadata, next: true);
	// 		await payload.WriteFlush(Transport.Output, item.data, item.metadata, cancellationToken);
	// 	}
	//
	// 	var finalPayload = new RSocketProtocol.Payload(streamId, complete: true);
	// 	await finalPayload.WriteFlush(Transport.Output, cancel: cancellationToken);
	// }
}

