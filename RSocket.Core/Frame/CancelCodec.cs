namespace RSocket.Frame;

public readonly ref struct CancelCodec : IFrameBody
{
	public readonly HeaderCodec Header { get; }
	public readonly Int32 StreamId => Header.StreamId;
	public readonly int MetadataLength => 0;
	public readonly int DataLength => 0;
	public readonly int InnerLength => 0;
	public readonly int Length => IFrameBody.GetLength(this);

	public CancelCodec(FrameCodecInitializer initializer)
	{
		Header = new HeaderCodec(
			frameType: Consts.FrameType.Cancel,
			streamId: 0,
			otherFlags: Consts.HeaderFlags.None);
	}

	public CancelCodec(HeaderCodec header)
	{
		Header = header;
	}

	public void Encode(FrameBuffer writer)
	{
		IFrameBody.EncodeHeader(this, writer);
	}

	public override string ToString() => $"{Header} Metadata[{MetadataLength}], Data[{DataLength}]";
}
