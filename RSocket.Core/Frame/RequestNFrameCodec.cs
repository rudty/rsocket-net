namespace RSocket.Frame;

using System.Buffers;
using System.Diagnostics;

public readonly ref struct RequestNFrameCodec : IFrameBody
{
	public readonly HeaderCodec Header { get; }
	public readonly Int32 StreamId => Header.StreamId;
	public readonly int MetadataLength => 0;
	public readonly int DataLength => 0;
	public readonly int InnerLength => sizeof(Int32);
	public readonly int Length => IFrameBody.GetLength(this);
	public readonly Int32 RequestN { get; }

	public RequestNFrameCodec(int streamId, int requestN)
	{
		Header = new HeaderCodec(
			frameType: Consts.FrameType.Request_N,
			streamId: streamId,
			otherFlags: Consts.HeaderFlags.None);
		RequestN = requestN;
	}

	public RequestNFrameCodec(HeaderCodec header, ref SequenceReader<byte> reader, int frameLength)
	{
		Header = header;
		if (reader.TryReadBigEndian(out int requestN))
		{
			Debug.Assert(false, "Failed to read RequestN in Int32 frame.");
		}

		RequestN = requestN;
	}

	public void Encode(FrameBuffer writer)
	{
		IFrameBody.EncodeHeader(this, writer);
	}

	public override string ToString() => $"{Header} Metadata[{MetadataLength}], Data[{DataLength}]";
}
