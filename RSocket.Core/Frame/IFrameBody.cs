namespace RSocket.Frame;

using System.Runtime.CompilerServices;

public interface IFrameBody
{
	/// <summary>
	/// 메세지 헤더
	/// </summary>
	HeaderCodec Header { get; }

	/// <summary>
	/// 패킷 자체적인 직렬화 길이
	/// </summary>
	int InnerLength { get; }

	/// <summary>
	/// 메타 데이터 직렬화 길이
	/// </summary>
	int MetadataLength { get; }

	/// <summary>
	/// Body 직렬화 길이
	/// </summary>
	int DataLength { get; }

	/// <summary>
	/// 직렬화 후 길이
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int GetLength<T>(T frameBody) where T : IFrameBody, allows ref struct
	{
		return frameBody.Header.LengthWithMetadataHeader + frameBody.InnerLength + frameBody.MetadataLength + frameBody.DataLength;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int ReceiveDataLength<T>(T frameBody, int frameLength) where T : IFrameBody, allows ref struct
	{
		frameLength -= frameBody.Header.LengthWithMetadataHeader;
		frameLength -= frameBody.InnerLength;
		frameLength -= frameBody.MetadataLength;
		return frameLength;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int EncodeHeader<T>(T frameBody, FrameBuffer writer) where T : IFrameBody, allows ref struct
	{
		var bodyLength = GetLength(frameBody);
		return frameBody.Header.Encode(writer, bodyLength);
	}

	public static int EncodeMetadataAndData<T>(T frameBody, FrameBuffer writer, Memory<byte> metadata, Memory<byte> data) where T : IFrameBody, allows ref struct
	{
		var written = 0;
		written += EncodeMetadata(frameBody, writer, metadata);
		written += EncodeData(frameBody, writer, data);
		return written;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int EncodeMetadata<T>(T frameBody, FrameBuffer writer, Memory<byte> metadata) where T : IFrameBody, allows ref struct
	{
		var written = 0;
		if (frameBody.Header.HasMetadata)
		{
			written += writer.WriteInt24BigEndian(frameBody.MetadataLength);
			written += writer.Write(metadata.Span);
		}

		return written;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]

	private static int EncodeData<T>(T frameBody, FrameBuffer writer, Memory<byte> data) where T : IFrameBody, allows ref struct
	{
		return writer.Write(data.Span);
	}
}
