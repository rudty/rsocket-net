namespace Test.Frame;

using System.Buffers;
using System.Diagnostics;
using System.Text;
using RSocket;
using RSocket.Frame;
using Xunit;

public class PayloadCodecTest
{
	[Fact]
	public void SerializeDeserialize_PayloadCodec_WithMetadata()
	{
		var streamId = 0x01020304;
		var metadataBytes = Encoding.ASCII.GetBytes("meta");
		var dataBytes = Encoding.ASCII.GetBytes("hello");
		var metadata = new Memory<byte>(metadataBytes);
		var data = new Memory<byte>(dataBytes);

		var initializer = FrameCodecInitializer.NewPayload(
			streamId: streamId,
			follows: false,
			complete: true,
			next: true,
			dataLength: data.Length,
			metadataLength: metadata.Length);
		var payload = new ZeroSizeFrameCodec(initializer);

		var buffer = FrameBuffer.Create(64);
		payload.Encode(buffer, metadata, data);

		var seq = new ReadOnlySequence<byte>(buffer.ReadOnlyMemory);
		var reader = new SequenceReader<byte>(seq);

		Assert.True(reader.TryReadUInt24BigEndian(out var frameLength));

		var header = new HeaderCodec(ref reader);
		var decoded = new ZeroSizeFrameCodec(header, ref reader, frameLength);

		Assert.Equal(metadata.Length, decoded.MetadataLength);
		Assert.Equal(data.Length, decoded.DataLength);
		Assert.True(header.HasMetadata);

		var readMetaSeq = decoded.ReadMetadata(reader);
		var readDataSeq = decoded.ReadData(reader);

		byte[] ToArray(ReadOnlySequence<byte> s)
		{
			var arr = new byte[s.Length];
			s.CopyTo(arr);
			return arr;
		}

		Assert.Equal(metadataBytes, ToArray(readMetaSeq));
		Assert.Equal(dataBytes, ToArray(readDataSeq));

		buffer.Release();
		Debug.Assert(!buffer.IsInitialized);
	}

	[Fact]
	public void SerializeDeserialize_PayloadCodec_WithoutMetadata()
	{
		var streamId = 0x0A0B0C0D;
		var metadata = Memory<byte>.Empty;
		var dataBytes = Encoding.ASCII.GetBytes("payload");
		var data = new Memory<byte>(dataBytes);

		var initializer = FrameCodecInitializer.NewPayload(
			streamId: streamId,
			follows: false,
			complete: true,
			next: true,
			dataLength: data.Length,
			metadataLength: metadata.Length);
		var payload = new ZeroSizeFrameCodec(initializer);

		var buffer = FrameBuffer.Create(64);
		payload.Encode(buffer, metadata, data);

		var seq = new ReadOnlySequence<byte>(buffer.ReadOnlyMemory);
		var reader = new SequenceReader<byte>(seq);

		Assert.True(reader.TryReadUInt24BigEndian(out var frameLength));

		var header = new HeaderCodec(ref reader);
		var decoded = new ZeroSizeFrameCodec(header, ref reader, frameLength);

		Assert.Equal(0, decoded.MetadataLength);
		Assert.Equal(data.Length, decoded.DataLength);
		Assert.False(header.HasMetadata);

		var readDataSeq = decoded.ReadData(reader);

		byte[] ToArray(ReadOnlySequence<byte> s)
		{
			var arr = new byte[s.Length];
			s.CopyTo(arr);
			return arr;
		}

		Assert.Equal(dataBytes, ToArray(readDataSeq));

		buffer.Release();
		Debug.Assert(!buffer.IsInitialized);
	}
}
