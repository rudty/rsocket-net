namespace RSocket.Frame;

using global::RSocket.Payloads;
using System;
using System.Buffers;

public readonly ref struct HeaderCodec
{
	public const int Length = sizeof(Int32) + sizeof(UInt16);
	private const int FRAMETYPE_OFFSET = 10;
	private const ushort FRAMETYPE_TYPE = 0b_111111 << FRAMETYPE_OFFSET;

	public readonly Int32 streamId;
	public readonly UInt16 frameTypeAndFlags;

	public HeaderCodec(Consts.FrameType frameType, Int32 streamId, int metadataLength, Consts.HeaderFlags otherFlags = 0)
	{
		var flagLocal = ((int)frameType << FRAMETYPE_OFFSET) & FRAMETYPE_TYPE;
		this.streamId = streamId;
		if (metadataLength > 0)
		{
			flagLocal |= (int)Consts.HeaderFlags.Metadata;
		}

		flagLocal |= (int)otherFlags;
		frameTypeAndFlags = (ushort)flagLocal;
	}

	public HeaderCodec(ref SequenceReader<byte> reader)
	{
		reader.TryReadBigEndian(out streamId);
		reader.TryReadBigEndian(out frameTypeAndFlags);
	}

	public readonly Int32 StreamId => streamId;
	public readonly UInt16 RawFrameTypeAndFlags => frameTypeAndFlags;
	public readonly UInt16 Flags => (UInt16)(frameTypeAndFlags & (int)Consts.HeaderFlags.Mask);
	public readonly Consts.FrameType FrameType => (Consts.FrameType)((frameTypeAndFlags & FRAMETYPE_TYPE) >> FRAMETYPE_OFFSET);
	public readonly bool HasFollows => Consts.HasFlag(frameTypeAndFlags, Consts.HeaderFlags.Follows);
	public readonly bool IsComplete => Consts.HasFlag(frameTypeAndFlags, Consts.HeaderFlags.Complete);
	public readonly bool IsNext => Consts.HasFlag(frameTypeAndFlags, Consts.HeaderFlags.Next);
	public readonly bool HasMetadata => Consts.HasFlag(frameTypeAndFlags, Consts.HeaderFlags.Metadata);

	public readonly int LengthWithMetadataHeader
	{
		get
		{
			if (HasMetadata)
			{
				return Length + Consts.SizeOfMetadataLength;

			}

			return Length;
		}
	}

	public readonly int Write(FrameBuffer writer, int length)
	{
		writer.WriteInt24BigEndian(length); // Not included in total length.
		writer.WriteInt32BigEndian(streamId);
		writer.WriteUInt16BigEndian(frameTypeAndFlags);

		return Length;
	}
	
	public override readonly string ToString() => $"{streamId:0000} {FrameType}";
}
