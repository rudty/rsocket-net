using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket.Transports
{
	using System.Net.Sockets;
	using System.Net.WebSockets;

	internal static partial class RSocketTransportExtensions
	{
		//Framing helper methods. They stay close to the original code as called, but add and remove the framing at the transport-socket boundary.
		public static Memory<byte> GetMemory(this PipeWriter output, out Memory<byte> memoryframe, bool haslength = false) { memoryframe = output.GetMemory(); return haslength ? memoryframe : memoryframe.Slice(RSocketProtocol.MESSAGEFRAMESIZE); }
		public static void Advance(this PipeWriter output, int bytes, bool endOfMessage, in Memory<byte> memoryframe) { RSocketProtocol.MessageFrameWrite(bytes, endOfMessage, memoryframe.Span); output.Advance(RSocketProtocol.MESSAGEFRAMESIZE + bytes); }

		//public static async ValueTask<SequencePosition> SendAsync(this WebSocket socket, ReadOnlySequence<byte> buffer, SequencePosition position, WebSocketMessageType webSocketMessageType, CancellationToken cancellationToken = default)
		//{
		//	for (var frame = PeekFrame(buffer.Slice(position)); frame.Length > 0; frame = PeekFrame(buffer.Slice(position)))
		//	{
		//		//Console.WriteLine($"Send Frame[{frame.Length}]");
		//		var offset = buffer.GetPosition(RSocketProtocol.MESSAGEFRAMESIZE, position);
		//		if (buffer.Slice(offset).Length < frame.Length) { break; }    //If there is a partial message in the buffer, yield to accumulate more. Can't compare SequencePositions...
		//		await socket.SendAsync(buffer.Slice(offset, frame.Length), webSocketMessageType, cancellationToken);
		//		position = buffer.GetPosition(frame.Length, offset);
		//	}

		//	//TODO Length may actually be on a boundary... Should use a reader.
		//	//while (buffer.TryGet(ref position, out var memory, advance: false))
		//	//{
		//	//	var (length, isEndOfMessage) = RSocketProtocol.MessageFrame(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(memory.Span));
		//	//	var offset = buffer.GetPosition(sizeof(int), position);
		//	//	if (buffer.Slice(offset).Length < length) { break; }	//If there is a partial message in the buffer, yield to accumulate more.
		//	//	await socket.SendAsync(buffer.Slice(offset, length), webSocketMessageType, cancellationToken);
		//	//	position = buffer.GetPosition(length, offset);
		//	//}
		//	return position;
		//}
	}
}
