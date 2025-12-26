// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Buffers
{
	public static partial class SequenceReaderExtensions
	{
		/// <summary>
		/// Try to read the given type out of the buffer if possible. Warning: this is dangerous to use with arbitrary
		/// structs- see remarks for full details.
		/// </summary>
		/// <remarks>
		/// IMPORTANT: The read is a straight copy of bits. If a struct depends on specific state of it's members to
		/// behave correctly this can lead to exceptions, etc. If reading endian specific integers, use the explicit
		/// overloads such as <see cref="TryReadLittleEndian(ref SequenceReader{byte}, out short)"/>
		/// </remarks>
		/// <returns>
		/// True if successful. <paramref name="value"/> will be default if failed (due to lack of space).
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static unsafe bool TryRead<T>(ref this SequenceReader<byte> reader, out T value) 
			where T : unmanaged, IBinaryInteger<T>
        {
            int size = Unsafe.SizeOf<T>();
            ReadOnlySpan<byte> span = reader.UnreadSpan;

            // Fast Path: 현재 세그먼트에 충분한 데이터가 있는 경우
            if (span.Length >= size)
            {
                value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(span));
                reader.Advance(size);
                return true;
            }

            // Slow Path: 여러 세그먼트에 걸쳐 있는 경우
            return TryReadMultisegment(ref reader, out value);
        }

		private static unsafe bool TryReadMultisegment<T>(ref SequenceReader<byte> reader, out T value) 
			where T : unmanaged, IBinaryInteger<T>
        {
            int size = Unsafe.SizeOf<T>();
            Span<byte> tempSpan = stackalloc byte[size];

            if (reader.TryCopyTo(tempSpan))
            {
                value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(tempSpan));
                reader.Advance(size);
                return true;
            }

            value = default;
            return false;
        }

		/// <summary>
		/// Reads an <see cref="Int16"/> as little endian.
		/// </summary>
		/// <returns>False if there wasn't enough data for an <see cref="Int16"/>.</returns>
		public static bool TryReadLittleEndian(ref this SequenceReader<byte> reader, out ushort value)
		{
			if (BitConverter.IsLittleEndian)
			{
				return reader.TryRead(out value);
			}

			return TryReadReverseEndianness(ref reader, out value);
		}

		/// <summary>
		/// Reads an <see cref="Int16"/> as big endian.
		/// </summary>
		/// <returns>False if there wasn't enough data for an <see cref="Int16"/>.</returns>
		public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out ushort value)
		{
			if (!BitConverter.IsLittleEndian)
			{
                return reader.TryRead(out value);
			}

			return TryReadReverseEndianness(ref reader, out value);
		}

		private static bool TryReadReverseEndianness(ref SequenceReader<byte> reader, out ushort value)
		{
			if (reader.TryRead(out value))
			{
				value = BinaryPrimitives.ReverseEndianness(value);
				return true;
			}

			return false;
		}
	}
}