namespace RSocket.Frame;

using global::RSocket.Payloads;
using System.Buffers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using static global::RSocket.RSocketProtocol;

public readonly ref struct SetupCodec
{
	private readonly HeaderCodec Header;
	public readonly Int32 StreamId => Header.StreamId;
	public readonly UInt16 MajorVersion { get; }
	public readonly UInt16 MinorVersion { get; }
	public readonly Int32 KeepAlive { get; }
	public readonly Int32 Lifetime { get; }

	public readonly byte[] ResumeToken { get; }
	public readonly string MetadataMimeType { get; }
	public readonly string DataMimeType { get; }
	public readonly int MetadataLength { get; }
	public readonly int DataLength { get; }


	public SetupCodec(Int32 keepalive, Int32 lifetime, string? metadataMimeType, string? dataMimeType, byte[]? resumeToken, int dataLength, int metadataLength)
	{
		var headerFlags = Consts.HeaderFlags.None;
		if (resumeToken is not null && resumeToken.Length > 0)
		{
			headerFlags |= Consts.HeaderFlags.SetupResume;
		}

		Header = new HeaderCodec(
			frameType: Consts.FrameType.Setup,
			streamId: 0,
			metadataLength: metadataLength,
			headerFlags);

		MajorVersion = MAJOR_VERSION;
		MinorVersion = MINOR_VERSION;
		KeepAlive = keepalive;
		Lifetime = lifetime;
		ResumeToken = resumeToken ?? Array.Empty<byte>();
		MetadataMimeType = metadataMimeType ?? RSocketOptions.Default.MetadataMimeType;
		DataMimeType = dataMimeType ?? RSocketOptions.Default.DataMimeType;
		MetadataLength = metadataLength;
		DataLength = dataLength;
	}

	public SetupCodec(in HeaderCodec header, ref SequenceReader<byte> reader, int frameLength)
	{
		Header = header;
		reader.TryReadBigEndian(out UInt16 majorVersion);
		MajorVersion = majorVersion;
		reader.TryReadBigEndian(out UInt16 minorVersion);
		MinorVersion = minorVersion;
		reader.TryReadBigEndian(out Int32 keepAlive);
		KeepAlive = keepAlive;
		reader.TryReadBigEndian(out Int32 lifetime);
		Lifetime = lifetime;

		if (HasResume)      //TODO Duplicate test logic here
		{
			reader.TryReadBigEndian(out UInt16 resumeTokenLength);
			ResumeToken = new byte[resumeTokenLength];
			reader.TryRead(ResumeToken.AsSpan());
		}
		else
		{
			ResumeToken = Array.Empty<byte>();
		}

		if (!reader.TryReadPrefix(out var localMetadataMimeType))
		{
			throw new InvalidOperationException("Failed to read MetadataMimeType in Setup frame.");
		}

		if (!reader.TryReadPrefix(out var localDataMimeType))
		{
			throw new InvalidOperationException("Failed to read DataMimeType in Setup frame.");
		}

		MetadataMimeType = localMetadataMimeType;
		DataMimeType = localDataMimeType;

		if (header.HasMetadata && reader.TryReadUInt24BigEndian(out var length))
		{
			MetadataLength = length;
		}

		DataLength = frameLength - header.LengthWithMetadataHeader - InnerLength - MetadataLength;
	}

	private readonly int InnerLength =>
		sizeof(UInt16) + // MajorVersion
		sizeof(UInt16) + // MinorVersion
		sizeof(Int32) + // KeepAlive
		sizeof(Int32) + // Lifetime
		(HasResume ? ResumeToken.Length : 0) +
		sizeof(byte) + MetadataMimeType.Length +
		sizeof(byte) + DataMimeType.Length ;

	public readonly int Length => Header.LengthWithMetadataHeader + InnerLength + MetadataLength + DataLength;
	public readonly bool HasResume => Consts.HasFlag(Header.RawFrameTypeAndFlags, Consts.HeaderFlags.SetupResume);
	public readonly bool CanLease => Consts.HasFlag(Header.RawFrameTypeAndFlags, Consts.HeaderFlags.SetupLease);

	public void Encode(FrameBuffer frameBuffer, Memory<byte> data, Memory<byte> metadata)
	{
		var written = Header.Write(frameBuffer, Length);
		written += frameBuffer.WriteUInt16BigEndian(MajorVersion);
		written += frameBuffer.WriteUInt16BigEndian(MinorVersion);
		written += frameBuffer.WriteInt32BigEndian(KeepAlive);
		written += frameBuffer.WriteInt32BigEndian(Lifetime);

		if (HasResume)
		{
			written += frameBuffer.WriteUInt16BigEndian(ResumeToken.Length);
			written += frameBuffer.Write(ResumeToken);
		}

		written += frameBuffer.WritePrefixByte(MetadataMimeType);
		written += frameBuffer.WritePrefixByte(DataMimeType);

		if (Header.HasMetadata)
		{
			written += frameBuffer.WriteInt24BigEndian(MetadataLength);
			written += frameBuffer.Write(metadata.Span);
		}

		written += frameBuffer.Write(data.Span);

		Debug.Assert(written == Length);
	}

	public ReadOnlySequence<byte> ReadMetadata(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Position, MetadataLength);
	public ReadOnlySequence<byte> ReadData(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Sequence.GetPosition(MetadataLength, reader.Position), DataLength);
	public void Read(ref SequenceReader<byte> reader, out ReadOnlySequence<byte> metadata, out ReadOnlySequence<byte> data)
	{
		metadata = ReadMetadata(reader);
		data = ReadData(reader);
		reader.Advance(metadata.Length + data.Length);
	}
}

