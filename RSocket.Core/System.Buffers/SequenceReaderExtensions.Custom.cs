// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Buffers
{
	public static partial class SequenceReaderExtensions
	{
        /// <summary>Attempts to read a <see cref="Span{byte}"/> from a sequence.</summary>
        /// <param name="reader">The reader to extract the Span from; the reader will advance.</param>
        /// <param name="destination">The destination Span.</param>
        /// <returns>True if the sequence had enough to fill the span.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRead(this ref SequenceReader<byte> reader, scoped Span<byte> destination)
        {
            if (reader.TryCopyTo(destination))
            {
                reader.Advance(destination.Length);
                return true;
            }

            return false;
        }

        /// <summary>Attempt to read a byte-prefixed string from a sequence.</summary>
        /// <param name="reader">The reader to extract the string from; the reader will advance.</param>
        /// <param name="value">The resulting value.</param>
        /// <param name="encoding">The Encoding of the string bytes. Defaults to ASCII.</param>
        /// <returns>True if the sequence had enough to fill the string.</returns>
        public static bool TryReadPrefix(ref this SequenceReader<byte> reader, out string value, Encoding encoding = default)
        {
            // 1. 최소 길이(1바이트) 체크 및 길이 접두사 엿보기
            var remaining = reader.Remaining;
            if (remaining < 1)
            {
                value = null;
                return false;
            }

            var unread = reader.UnreadSpan;
            var length = unread[0];

            // 2. 빈 문자열 특수 케이스 처리 (1바이트 소모 필수)
            if (length == 0)
            {
                value = string.Empty;
                reader.Advance(1);
                return true;
            }

            // 3. 전체 페이로드(접두사 1 + 데이터 n) 가용성 확인
            if (remaining < length + 1) // 1 byte for length prefix
            {
                value = null;
                return false;
            }

            encoding ??= Encoding.ASCII;

            // 4. Fast Path: 현재 세그먼트에 데이터가 연속되어 있는 경우
            if (unread.Length >= length + 1) // 1 byte for length prefix
            {
                value = encoding.GetString(unread.Slice(1, length));
                reader.Advance(length + 1);
                return true;
            }

            // 5. Slow Path: 데이터가 세그먼트 경계에 걸쳐 있는 경우 (병합 복사)
            // 최대 256바이트 까지 사용하므로 stackalloc 사용
            Span<byte> buffer = stackalloc byte[length + 1]; // 1 byte for length prefix
            if (!reader.TryRead(buffer)) // 데이터가 남아있음이 확인되었으므로 항상 성공함
            {
                value = null;
                return false;
            }

            value = encoding.GetString(buffer.Slice(1));
            return true;
        }

        //TODO DOCS
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRead(ref this SequenceReader<byte> reader, out string value, int length, Encoding encoding = default)
        {
            // 1. 데이터 부족 시 조기 리턴
            if (reader.Remaining < length)
            {
                value = null;
                return false;
            }

            encoding ??= Encoding.UTF8;

            // 2. Fast Path: 데이터가 현재 세그먼트에 연속적으로 있는 경우
            // 복사 없이 ReadOnlySpan을 바로 사용하여 문자열 생성
            var unread = reader.UnreadSpan;
            if (unread.Length >= length)
            {
                value = encoding.GetString(unread.Slice(0, length));
                reader.Advance(length);
                return true;
            }

            // 3. Slow Path: 데이터가 세그먼트 경계에 걸쳐 있는 경우
            return TryReadStringSlow(ref reader, length, out value, encoding);
        }

        private static bool TryReadStringSlow(ref SequenceReader<byte> reader, int length, out string value, Encoding encoding)
        {
            const int StackAllocThreshold = 256;

            if (length <= StackAllocThreshold)
            {
                // 작은 길이는 stackalloc으로 힙 할당 없이 처리
                Span<byte> buffer = stackalloc byte[length];
                reader.TryCopyTo(buffer);
                value = encoding.GetString(buffer);
            }
            else
            {
                // 큰 길이는 ArrayPool을 사용하여 GC 부하 최소화
                byte[] rented = ArrayPool<byte>.Shared.Rent(length);
                try
                {
                    Span<byte> buffer = rented.AsSpan(0, length);
                    reader.TryCopyTo(buffer);
                    value = encoding.GetString(buffer);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }

            reader.Advance(length);
            return true;
        }

        /// <summary>
        /// Reads an <see cref="UInt24"/> as big endian;
        /// </summary>
        /// <returns>False if there wasn't enough data for an <see cref="UInt24"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadUInt24BigEndian(ref this SequenceReader<byte> reader, out int value)
        {
            // 1. 현재 세그먼트(UnreadSpan)에서 바로 읽을 수 있는 경우 (Fast Path)
            ReadOnlySpan<byte> unread = reader.UnreadSpan;
            if (unread.Length >= 3)
            {
                // 비트 연산을 통한 빠른 계산 (Big Endian)
                value = (unread[0] << 16) | (unread[1] << 8) | unread[2];
                reader.Advance(3);
                return true;
            }

            // 2. 세그먼트가 나뉘어 있거나 데이터가 부족한 경우 (Slow Path)
            // 남은 데이터가 3바이트 미만이면 즉시 종료
            if (reader.Remaining < 3)
            {
                value = default;
                return false;
            }

            // stackalloc을 사용하여 힙 할당 없이 임시 버퍼 생성
            Span<byte> buffer = stackalloc byte[3];

            // SequenceReader 내부의 데이터를 버퍼로 복사 (세그먼트 경계 자동 처리)
            if (reader.TryCopyTo(buffer))
            {
                value = (buffer[0] << 16) | (buffer[1] << 8) | buffer[2];
                reader.Advance(3);
                return true;
            }

            value = default;
            return false;
        }
    }
}