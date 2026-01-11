namespace Test.Frame;

using System.Buffers;
using System.Diagnostics;
using RSocket;
using RSocket.Frame;
using Xunit;

public class HeaderCodecTest
{
	[Fact]
	public void SerializeDeserialize_HeaderCodec()
	{
		var streamId = 0x0A0B0C0D;
		var metadataLength = 5;
		var otherFlags = Consts.HeaderFlags.Next | Consts.HeaderFlags.Follows;

		var header = new HeaderCodec(Consts.FrameType.Payload, streamId, metadataLength, otherFlags);

		var totalLength = 123; // arbitrary frame length
		var buffer = FrameBuffer.Create(64);
		header.Encode(buffer, totalLength);

		var seq = new ReadOnlySequence<byte>(buffer.ReadOnlyMemory);
		var reader = new SequenceReader<byte>(seq);

		// read frame length (UInt24 big endian)
		Assert.True(reader.TryReadUInt24BigEndian(out var readLength));
		Assert.Equal(totalLength, readLength);

		// decode header
		var decoded = new HeaderCodec(ref reader);

		Assert.Equal(header.StreamId, decoded.StreamId);
		Assert.Equal(header.FrameType, decoded.FrameType);
		Assert.Equal(header.RawFrameTypeAndFlags, decoded.RawFrameTypeAndFlags);
		Assert.Equal(header.HasMetadata, decoded.HasMetadata);
		Assert.Equal(header.HasFollows, decoded.HasFollows);
		Assert.Equal(header.IsNext, decoded.IsNext);

		buffer.Release();

		Debug.Assert(!buffer.IsInitialized);
	}
}
