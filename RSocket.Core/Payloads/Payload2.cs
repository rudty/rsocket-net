namespace RSocket.Payloads;

using System;
using System.Buffers;

public sealed class Payload2 : IDisposable
{
	public ReadOnlySequence<byte> Metadata { get; private set; }
	public ReadOnlySequence<byte> Data { get; private set; }

	private Payload2(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)
	{
		Data = data;
		Metadata = metadata;
	}

	public static Payload2 Create(byte[] data)
	{
		return new Payload2(new ReadOnlySequence<byte>(data), ReadOnlySequence<byte>.Empty);
	}

	public static Payload2 Create(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)
	{
		return new Payload2(data, metadata);
	}

	public void Dispose()
	{
		//TODO. OBJECT POOLING
	}
}
