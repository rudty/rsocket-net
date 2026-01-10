namespace RSocket.Frame;

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;

public sealed class FrameBuffer
{
	private volatile int referenceCount;
	private byte[]? buffer = null;
	private int used;
	private int size;

	private FrameBuffer()
	{
	}

	private void Initialize(byte[] buffer, int size)
	{
		referenceCount = 1;
		used = 0;
		this.buffer = buffer;
		this.size = size;
	}

	public static FrameBuffer Create(int size)
	{
		var outputBuffer = new FrameBuffer();
		var buffer = ArrayPool<byte>.Shared.Rent(size);
		outputBuffer.Initialize(buffer, size);

		return outputBuffer;
	}

	public bool IsInitialized => buffer is not null;
	public int ReferenceCount => referenceCount;

	public void Retain()
	{
		var newReferenceCount = Interlocked.Increment(ref referenceCount);
		Debug.Assert(newReferenceCount <= 1, "Retain called on disposed OutputBuffer");
	}

	public void Release()
	{
		var newReferenceCount = Interlocked.Decrement(ref referenceCount);
		if (newReferenceCount == 0)
		{
			var buffer = this.buffer;
			if (buffer is not null)
			{
				ArrayPool<byte>.Shared.Return(buffer);
				this.buffer = null;
				// TODO FrameBuffer ObjectPool
			}
		}
	}

	public ReadOnlyMemory<byte> ReadOnlyMemory => new(buffer, 0, used);

	private Span<byte> GetBuffer(int needed)
	{
		return new Span<byte>(buffer, used, needed);
	}

	public void Write(byte value)
	{
		Debug.Assert(buffer is not null, "buffer is null");
		buffer[used] = value;
		used += 1;
	}

	public int WriteUInt16BigEndian(int value) => WriteUInt16BigEndian((UInt16)value);

	public int WriteUInt16BigEndian(UInt16 value)
	{
		BinaryPrimitives.WriteUInt16BigEndian(GetBuffer(sizeof(UInt16)), value);
		used += sizeof(UInt16);
		return sizeof(UInt16);
	}

	public int WriteInt32BigEndian(Int32 value)
	{
		BinaryPrimitives.WriteInt32BigEndian(GetBuffer(sizeof(Int32)), value);
		used += sizeof(Int32);
		return sizeof(Int32);
	}

	public int WriteInt64BigEndian(Int64 value)
	{
		BinaryPrimitives.WriteInt64BigEndian(GetBuffer(sizeof(Int64)), value);
		used += sizeof(Int64);
		return sizeof(Int64);
	}

	public int WriteUInt32BigEndian(UInt32 value)
	{
		BinaryPrimitives.WriteUInt32BigEndian(GetBuffer(sizeof(UInt32)), value);
		used += sizeof(UInt32);
		return sizeof(UInt32);
	}

	public int WriteInt24BigEndian(int value)
	{
		const int size3Byte = 3;
		var span = GetBuffer(size3Byte);
		span[0] = (byte)(value >> 16);
		span[1] = (byte)(value >> 8);
		span[2] = (byte)value;
		used += size3Byte;
		return size3Byte;
	}

	public int Write(ReadOnlySpan<byte> values)
	{
		if (values.IsEmpty)
		{
			return 0;
		}

		var span = GetBuffer(values.Length);
		values.CopyTo(span);
		used += values.Length;
		return values.Length;
	}

	public int Write(ReadOnlySequence<byte> values)
	{
		if (values.IsSingleSegment)
		{
			return Write(values.First.Span);
		}

		var count = 0;
		foreach (var memory in values)
		{
			count += Write(memory.Span);
		}

		return count;
	}

	public int WritePrefixByte(string text)
	{
		Debug.Assert(text is not null);

		var bytesCount = text.Length; // ASCII Byte.Length == text.Length
		if (bytesCount > byte.MaxValue)
		{
			throw new ArgumentOutOfRangeException(nameof(text), text, $"String encoding [{bytesCount}] would exceed the maximum prefix length. [{byte.MaxValue}]");
		}

		var totalWriteCount = bytesCount + 1;
		var span = GetBuffer(totalWriteCount);
		span[0] = (byte)bytesCount;
		Encoding.ASCII.GetBytes(text, span.Slice(1));

		used += totalWriteCount;
		return totalWriteCount;
	}
}
