namespace RSocket;

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;

public sealed class BufferWriter
{
	private readonly IBufferWriter<byte> _bufferWriter;
	private Memory<byte> _memory;
	private int _used;

	public BufferWriter(IBufferWriter<byte> writer)
	{
		_bufferWriter = writer;
		_memory = Memory<byte>.Empty;
		_used = 0;
	}


	private Span<byte> GetBuffer(int needed)
	{
		Debug.Assert(needed > 0);

		var remaining = _memory.Length - _used;
		if (remaining >= needed)
		{
			return _memory.Span.Slice(_used);
		}

		if (_used > 0)
		{
			_bufferWriter.Advance(_used);
		}

		_memory = _bufferWriter.GetMemory(needed);
		_used = 0;

		return _memory.Span;
	}

	public void Write(byte value)
	{
		var span = GetBuffer(1);
		span[0] = value;
		_used += 1;
	}

	public int WriteUInt16BigEndian(int value) => WriteUInt16BigEndian((UInt16)value);

	public int WriteUInt16BigEndian(UInt16 value)
	{
		BinaryPrimitives.WriteUInt16BigEndian(GetBuffer(sizeof(UInt16)), value);
		_used += sizeof(UInt16);
		return sizeof(UInt16);
	}

	public int WriteInt32BigEndian(Int32 value)
	{
		BinaryPrimitives.WriteInt32BigEndian(GetBuffer(sizeof(Int32)), value);
		_used += sizeof(Int32);
		return sizeof(Int32);
	}

	public int WriteInt64BigEndian(Int64 value)
	{
		BinaryPrimitives.WriteInt64BigEndian(GetBuffer(sizeof(Int64)), value);
		_used += sizeof(Int64);
		return sizeof(Int64);
	}

	public int WriteUInt32BigEndian(UInt32 value)
	{
		BinaryPrimitives.WriteUInt32BigEndian(GetBuffer(sizeof(UInt32)), value);
		_used += sizeof(UInt32);
		return sizeof(UInt32);
	}

	public int WriteInt24BigEndian(int value)
	{
		const int size3Byte = 3;
		var span = GetBuffer(size3Byte);
		span[0] = (byte)(value >> 16);
		span[1] = (byte)(value >> 8);
		span[2] = (byte)value;
		_used += size3Byte;
		return size3Byte;
	}

	public int Write(ReadOnlySpan<byte> values)
	{
		var span = GetBuffer(values.Length);
		values.CopyTo(span);
		_used += values.Length;
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

		_used += totalWriteCount;
		return totalWriteCount;
	}

	public void Flush()
	{
		if (_used > 0)
		{
			_bufferWriter.Advance(_used);
			_memory = Memory<byte>.Empty;
			_used = 0;
		}
	}


	public static BufferWriter Get(IBufferWriter<byte> bufferWriter)
	{
		var writer = new BufferWriter(bufferWriter);
		return writer;
	}

	public static void Return(BufferWriter writer)
	{

	}
}
