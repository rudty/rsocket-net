namespace RSocket.Frame;

using System.Buffers;
using System.Diagnostics;

/// <summary>
/// Header(4 + 2), Data, Metadata 제외하고 자체적인 크기가 0인 Frame Codec
/// RequestChannel, RequestStream, RequestFireAndForget
/// </summary>
public readonly ref struct Int32FrameCodec : IFrameBody
{
	public readonly HeaderCodec Header { get; }
	public readonly Int32 StreamId => Header.StreamId;
	public readonly int MetadataLength { get; }
	public readonly int DataLength { get; }
	public readonly int InnerLength => sizeof(Int32);
	public readonly int Length => IFrameBody.GetLength(this);

	/// <summary>
	/// Initial Request N
	/// </summary>
	public readonly Int32 RequestN { get; }

	public Int32FrameCodec(FrameCodecInitializer initializer, int requestN)
	{
		Header = initializer.CreateHeader();
		RequestN = requestN;
		DataLength = initializer.DataLength;
		MetadataLength = initializer.MetadataLength;
	}

	public Int32FrameCodec(HeaderCodec header, ref SequenceReader<byte> reader, int frameLength)
	{
		Header = header;
		if (reader.TryReadBigEndian(out int requestN))
		{
			Debug.Assert(false, "Failed to read RequestN in Int32 frame.");
		}

		RequestN = requestN;

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

	public override string ToString() => $"{Header} Metadata[{MetadataLength}], Data[{DataLength}]";
}
