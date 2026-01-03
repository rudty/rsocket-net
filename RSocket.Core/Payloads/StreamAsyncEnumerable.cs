namespace RSocket.Payloads;

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

public sealed class StreamAsyncEnumerator<T> : IAsyncEnumerable<T>, IAsyncEnumerator<T>, IObserver<T>, IValueTaskSource<bool>
{
	private ManualResetValueTaskSourceCore<bool> _core = new();
	private readonly Queue<T> _buffer = new();
	private readonly object _gate = new();
	private CancellationTokenRegistration _cancelRegistration;

	private ExceptionDispatchInfo? _error;
	private bool _isDisposed;
	private bool _isWaiting; // 소비자가 데이터를 기다리고 있는지 여부
	private T? _current;

	public StreamAsyncEnumerator()
	{
	}

	public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
	{
		_cancelRegistration = cancellationToken.Register(() =>
		{
			lock (_gate)
			{
				_core.SetException(new OperationCanceledException(cancellationToken));
			}
		});
		return this;
	}

	public T Current => _current!;

	public ValueTask<bool> MoveNextAsync()
	{
		lock (_gate)
		{
			// 1. 버퍼에 데이터가 있으면 즉시 반환
			if (_buffer.Count > 0)
			{
				_current = _buffer.Dequeue();
				return new ValueTask<bool>(true);
			}

			// 2. 에러가 발생했다면 에러 던짐
			_error?.Throw();

			// 3. 이미 완료되었다면 false 반환
			if (_isDisposed)
			{
				return new ValueTask<bool>(false);
			}

			// 4. 데이터가 없다면 대기 상태로 진입
			_core.Reset();
			_isWaiting = true;
			return new ValueTask<bool>(this, _core.Version);
		}
	}

	public void OnNext(T value)
	{
		lock (_gate)
		{
			if (_isDisposed || _error is not null)
			{
				return;
			}

			if (_isWaiting)
			{
				// 기다리는 중이면 즉시 데이터 전달 및 대기 해제
				_isWaiting = false;
				_current = value;
				_core.SetResult(true);
			}
			else
			{
				// 기다리는 중이 아니면 큐에 보관
				_buffer.Enqueue(value);
			}
		}
	}

	public void OnError(Exception error)
	{
		lock (_gate)
		{
			_error = ExceptionDispatchInfo.Capture(error);
			if (_isWaiting)
			{
				_isWaiting = false;
				_core.SetException(error);
			}
		}
	}

	public void OnCompleted()
	{
		lock (_gate)
		{
			_isDisposed = true;
			if (_isWaiting)
			{
				_isWaiting = false;
				_core.SetResult(false);
			}
		}
	}

	public async ValueTask DisposeAsync()
	{
		CancellationTokenRegistration reg;

		lock (_gate)
		{
			// 이미 종료되었다면 중복 처리 방지
			if (_isDisposed && _buffer.Count == 0)
			{
				return;
			}

			_isDisposed = true;
			_buffer.Clear();

			// 1. 대기 중인 소비자(MoveNextAsync)가 있다면 깨워줍니다.
			if (_isWaiting)
			{
				_isWaiting = false;
				// 에러를 던지거나 false를 반환하여 안전하게 종료시킴
				_core.SetResult(false);
			}

			// 2. 등록 정보를 로컬 변수에 옮기고 필드는 초기화
			reg = _cancelRegistration;
			_cancelRegistration = default;
		}

		// 3. lock 밖에서 안전하게 등록 해제
		// DisposeAsync는 취소 콜백이 실행 중이라면 그 콜백이 끝날 때까지 비동기로 기다려줍니다.
		await reg.DisposeAsync();
	}

	// --- IValueTaskSource 구현부 (Core에 위임) ---

	public bool GetResult(short token)
	{
		lock (_gate)
		{
			return _core.GetResult(token);
		}
	}

	public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

	public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
	{
		_core.OnCompleted(continuation, state, token, flags);
	}
}
