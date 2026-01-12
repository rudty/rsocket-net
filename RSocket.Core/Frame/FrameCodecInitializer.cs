namespace RSocket.Frame;

public readonly struct FrameCodecInitializer(
	int streamId,
	Consts.FrameType frameType,
	Consts.HeaderFlags headerFlags,
	int dataLength,
	int metaDataLength)
{
	public readonly Consts.FrameType FrameType { get; } = frameType;
	public readonly Consts.HeaderFlags HeaderFlags { get; } = headerFlags;
	public readonly int StreamId { get; } = streamId;
	public readonly int DataLength { get; } = dataLength;
	public readonly int MetadataLength { get; } = metaDataLength;

	public readonly HeaderCodec CreateHeader()
	{
		return new HeaderCodec(
			frameType: FrameType,
			streamId: StreamId,
			otherFlags: HeaderFlags);
	}

	public static FrameCodecInitializer NewChannel(
		int streamId,
		bool follows,
		bool complete,
		int dataLength,
		int metadataLength)
	{
		return New(
			streamId: streamId,
			frameType: Consts.FrameType.Request_Channel,
			follows: follows,
			complete: complete,
			next: false,
			dataLength: dataLength,
			metadataLength: metadataLength);
	}

	public static FrameCodecInitializer NewPayload(
		int streamId,
		bool follows,
		bool complete,
		bool next,
		int dataLength,
		int metadataLength)
	{
		return New(
			streamId: streamId,
			frameType: Consts.FrameType.Payload,
			follows: follows,
			complete: complete,
			next: next,
			dataLength: dataLength,
			metadataLength: metadataLength);
	}

	public static FrameCodecInitializer NewRequestResponse(
		int streamId,
		bool follows,
		int dataLength,
		int metadataLength)
	{
		return New(
			streamId: streamId,
			frameType: Consts.FrameType.Request_Response,
			follows: follows,
			complete: false,
			next: false,
			dataLength: dataLength,
			metadataLength: metadataLength);
	}

	public static FrameCodecInitializer NewFireAndForget(
		int streamId,
		bool follows,
		int dataLength,
		int metadataLength)
	{
		return New(
			streamId: streamId,
			frameType: Consts.FrameType.Request_Fire_And_Forget,
			follows: follows,
			complete: false,
			next: false,
			dataLength: dataLength,
			metadataLength: metadataLength);
	}

	public static FrameCodecInitializer NewMetadataPush(
		int streamId,
		bool follows,
		int metadataLength)
	{
		return New(
			streamId: streamId,
			frameType: Consts.FrameType.Metadata_Push,
			follows: follows,
			complete: false,
			next: false,
			dataLength: 0,
			metadataLength: metadataLength);
	}

	private static FrameCodecInitializer New(
		int streamId,
		Consts.FrameType frameType,
		bool follows,
		bool complete,
		bool next,
		int dataLength,
		int metadataLength)
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

		return new FrameCodecInitializer(streamId, frameType, headerFlags, dataLength, metadataLength);
	}
}
