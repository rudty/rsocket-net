using System;
using System.Net;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Buffers;

namespace RSocket.Transports
{
	//TODO Readd transport logging - worth it during debugging.
	public class SocketTransport : IRSocketTransport
	{
		private IPEndPoint Endpoint;
		private Socket Socket;

		internal Task Running { get; private set; } = Task.CompletedTask;
		//private CancellationTokenSource Cancellation;
#pragma warning disable CS0649
		private volatile bool Aborted;      //TODO Implement cooperative cancellation (and remove warning suppression)
#pragma warning restore CS0649

		public Uri Url { get; private set; }
		private LoggerFactory Logger;

		IDuplexPipe Front, Back;
		public PipeReader Input => Front.Input;
		public PipeWriter Output => Front.Output;

		public SocketTransport(string url, PipeOptions? outputoptions = null, PipeOptions? inputoptions = null) : this(new Uri(url), outputoptions, inputoptions) { }
		public SocketTransport(Uri url, PipeOptions? outputoptions = null, PipeOptions? inputoptions = null, WebSocketOptions? options = null)
		{

			if (string.Compare(url.Scheme, "TCP", true) != 0)
			{
				throw new ArgumentException("Only TCP connections are supported.", nameof(url));
			}

			if (url.Port == -1)
			{
				throw new ArgumentException("TCP Port must be specified.", nameof(url));
			}

			Url = url;
			//Options = options ?? WebSocketsTransport.DefaultWebSocketOptions;
			Logger = new Microsoft.Extensions.Logging.LoggerFactory(new[] { new Microsoft.Extensions.Logging.Debug.DebugLoggerProvider() });
			(Front, Back) = DuplexPipe.CreatePair(outputoptions, inputoptions);
		}

		public async ValueTask StartAsync(CancellationToken cancellationToken = default)
		{
			var dns = await Dns.GetHostEntryAsync(Url.Host);
			if (dns.AddressList.Length is 0)
			{
				throw new InvalidOperationException($"Unable to resolve address.");
			}

			Endpoint = new IPEndPoint(dns.AddressList[0], Url.Port);
			Socket = new Socket(Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			await Socket.ConnectAsync(dns.AddressList, Url.Port, cancellationToken);

			Running = ProcessSocketAsync(Socket, cancellationToken);
		}

		public ValueTask StopAsync() => ValueTask.CompletedTask;		//TODO More graceful shutdown

		private async Task ProcessSocketAsync(Socket socket, CancellationToken cancellationToken)
		{
			// Begin sending and receiving. Receiving must be started first because ExecuteAsync enables SendAsync.
			var receiving = StartReceiving(socket, cancellationToken);
			var sending = StartSending(socket, cancellationToken);

			var trigger = await Task.WhenAny(receiving, sending);

			//if (trigger == receiving)
			//{
			//	Log.WaitingForSend(_logger);

			//	// We're waiting for the application to finish and there are 2 things it could be doing
			//	// 1. Waiting for application data
			//	// 2. Waiting for a websocket send to complete

			//	// Cancel the application so that ReadAsync yields
			//	_application.Input.CancelPendingRead();

			//	using (var delayCts = new CancellationTokenSource())
			//	{
			//		var resultTask = await Task.WhenAny(sending, Task.Delay(_options.CloseTimeout, delayCts.Token));

			//		if (resultTask != sending)
			//		{
			//			// We timed out so now we're in ungraceful shutdown mode
			//			Log.CloseTimedOut(_logger);

			//			// Abort the websocket if we're stuck in a pending send to the client
			//			_aborted = true;

			//			socket.Abort();
			//		}
			//		else
			//		{
			//			delayCts.Cancel();
			//		}
			//	}
			//}
			//else
			//{
			//	Log.WaitingForClose(_logger);

			//	// We're waiting on the websocket to close and there are 2 things it could be doing
			//	// 1. Waiting for websocket data
			//	// 2. Waiting on a flush to complete (backpressure being applied)

			//	using (var delayCts = new CancellationTokenSource())
			//	{
			//		var resultTask = await Task.WhenAny(receiving, Task.Delay(_options.CloseTimeout, delayCts.Token));

			//		if (resultTask != receiving)
			//		{
			//			// Abort the websocket if we're stuck in a pending receive from the client
			//			_aborted = true;

			//			socket.Abort();

			//			// Cancel any pending flush so that we can quit
			//			_application.Output.CancelPendingFlush();
			//		}
			//		else
			//		{
			//			delayCts.Cancel();
			//		}
			//	}
			//}
		}

		private async Task StartReceiving(Socket socket, CancellationToken cancellationToken)
		{ 
			try
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					var memory = Back.Output.GetMemory();
					var received = await socket.ReceiveAsync(memory, SocketFlags.None, cancellationToken);
					Back.Output.Advance(received);
					var flushResult = await Back.Output.FlushAsync(cancellationToken);
					if (flushResult.IsCanceled || flushResult.IsCompleted)
					{
						break;
					}
				}
			}
			catch (SocketException e) when (e.SocketErrorCode == SocketError.OperationAborted)
			{
				return;
			}
			catch (OperationCanceledException)
			{
				// Ignore aborts, don't treat them like transport errors
			}
			catch (Exception e)
			{
				if (!Aborted && !cancellationToken.IsCancellationRequested)
				{
					Back.Output.Complete(e);
					throw;
				}
			}
			finally
			{
				try
				{
					Back.Output.Complete();
				} catch { }
			}
		}

		private async Task StartSending(Socket socket, CancellationToken cancellationToken)
		{
			Exception? error = null;

			try
			{
				while (true)
				{
					var result = await Back.Input.ReadAsync(cancellationToken);
					var buffer = result.Buffer;
					var consumed = buffer.Start;        //RSOCKET Framing

					try
					{
						if (result.IsCanceled || result.IsCompleted)
						{
							break;
						}

						if (!buffer.IsEmpty)
						{
							try
							{
								//Log.SendPayload(_logger, buffer.Length);
								consumed = await socket.SendAsync(buffer, buffer.Start, SocketFlags.None);     //RSOCKET Framing
							}
							catch (Exception)
							{
								if (!Aborted)
								{
									/*Log.ErrorWritingFrame(_logger, ex);*/
								}

								break;
							}
						}
					}
					finally
					{
						Back.Input.AdvanceTo(consumed, buffer.End);     //RSOCKET Framing
					}
				}
			}
			catch (Exception ex)
			{
				error = ex;
			}
			finally
			{
				Back.Input.Complete();
			}
		}

		static (int Length, bool IsEndOfMessage) PeekFrame(ReadOnlySequence<byte> sequence)
		{
			var reader = new SequenceReader<byte>(sequence);
			return reader.TryRead(out byte b1) && reader.TryRead(out byte b2) && reader.TryRead(out byte b3) ? ((b1 << 8 * 2) | (b2 << 8 * 1) | (b3 << 8 * 0), true) : (0, false);
		}

		public static async ValueTask<SequencePosition> SendAsync(this Socket socket, ReadOnlySequence<byte> buffer, SequencePosition position, SocketFlags socketFlags, CancellationToken cancellationToken = default)
		{
			for (var frame = PeekFrame(buffer.Slice(position)); frame.Length > 0; frame = PeekFrame(buffer.Slice(position)))
			{
				//Console.WriteLine($"Send Frame[{frame.Length}]");
				var length = frame.Length + RSocketProtocol.FRAMELENGTHSIZE;
				var offset = buffer.GetPosition(RSocketProtocol.MESSAGEFRAMESIZE - RSocketProtocol.FRAMELENGTHSIZE, position);
				if (buffer.Slice(offset).Length < length)
				{ break; }    //If there is a partial message in the buffer, yield to accumulate more. Can't compare SequencePositions...
				await socket.SendAsync(buffer.Slice(offset, length), socketFlags, cancellationToken);
				position = buffer.GetPosition(length, offset);
			}
			return position;
		}

		public static async ValueTask<int> SendReadOnlySequenceAsync(Socket socket, ReadOnlySequence<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken)
		{
			if (buffer.IsSingleSegment)
			{
				return await socket.SendAsync(buffer.First, socketFlags, cancellationToken);
			}

			var sent = 0;
			foreach (var memory in buffer)
			{
				sent += await socket.SendAsync(memory, socketFlags, cancellationToken);
			}

			return sent;
		}
	}
}
