namespace RSocket.Frame;

using System;
using System.Buffers;

public readonly ref struct PayloadCodec : IFrameBody
{
	public readonly HeaderCodec Header { get; }
	public readonly Int32 StreamId => Header.StreamId;
	public readonly int MetadataLength { get; }
	public readonly int DataLength { get; }
	public readonly int InnerLength => 0;
	public readonly int Length => IFrameBody.GetLength(this);

	public PayloadCodec(int streamId, Consts.FrameType frameType, int dataLength, int metadataLength, bool follows = false, bool complete = false, bool next = false)    //TODO Parameter ordering, isn't Next much more likely than C or F?
	{
		var headerFlags = Consts.HeaderFlags.None;
		if (follows)
		{
			headerFlags |= Consts.HeaderFlags.Follows;
		}

		if (complete)
		{
			headerFlags |= Consts.HeaderFlags.Complete;
		}

		if (next)
		{
			headerFlags |= Consts.HeaderFlags.Next;
		}

		if (metadataLength > 0)
		{
			headerFlags |= Consts.HeaderFlags.Metadata;
		}

		Header = new HeaderCodec(
			frameType: frameType,
			streamId: streamId,
			metadataLength: metadataLength,
			headerFlags);
		DataLength = dataLength;
		MetadataLength = metadataLength;
	}

	public PayloadCodec(HeaderCodec header, ref SequenceReader<byte> reader, int frameLength)
	{
		Header = header;
		
		if (header.HasMetadata && reader.TryReadUInt24BigEndian(out var length))
		{
			MetadataLength = length;
		}

		DataLength = IFrameBody.ReceiveDataLength(this, frameLength);
	}

	public void Encode(FrameBuffer writer, Memory<byte> metadata, Memory<byte> data)
	{
		IFrameBody.EncodeHeader(this, writer);
		IFrameBody.EncodeMetadataAndData(this, writer, metadata, data);
	}

	public ReadOnlySequence<byte> ReadMetadata(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Position, MetadataLength);
	public ReadOnlySequence<byte> ReadData(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Sequence.GetPosition(MetadataLength, reader.Position), DataLength);

	public override string ToString() => $"{Header.ToString()} Metadata[{MetadataLength}], Data[{DataLength}]";
}
