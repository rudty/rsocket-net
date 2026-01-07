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
	private bool _isCompleted;
	private bool _isWaiting; // 소비자가 데이터를 기다리고 있는지 여부
	private T? _current;

	/// <summary>
	/// GetAsyncEnumerator 중복 호출 제거
	/// </summary>
	private bool _isUsed;

	public StreamAsyncEnumerator()
	{
	}

	public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();

		if (_isUsed)
		{
			throw new InvalidOperationException("This enumerable can only be consumed once.");
		}

		_isUsed = true;

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
			ThrowIfDisposed();

			// 1. 버퍼에 데이터가 있으면 즉시 반환
			if (_buffer.Count > 0)
			{
				_current = _buffer.Dequeue();
				return new ValueTask<bool>(true);
			}

			// 2. 에러가 발생했다면 에러 던짐
			_error?.Throw();

			// 3. 이미 완료되었다면 false 반환
			if (_isCompleted)
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
			if (_isCompleted || _error is not null)
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
		DoComplete();
	}

	private void DoComplete()
	{
		lock (_gate)
		{
			_isCompleted = true;
			if (_isWaiting)
			{
				_isWaiting = false;
				try
				{
					_core.SetResult(false);
				}
				catch { }
			}
		}
	}

	public async ValueTask DisposeAsync()
	{
		CancellationTokenRegistration reg;

		lock (_gate)
		{
			// 1. 이미 Dispose가 완료되었다면 중복 실행 방지
			if (_isDisposed)
			{
				return;
			}

			_isDisposed = true;

			// 2. 스트림 상태 정리
			_buffer.Clear();
			_current = default!;

			// 3. 아직 MoveNextAsync로 대기 중인 소비자가 있다면 깨워줌
			DoComplete();

			// 4. 등록 정보를 로컬로 옮기고 필드 초기화
			reg = _cancelRegistration;
			_cancelRegistration = default;
		}

		// 5. lock 밖에서 안전하게 토큰 등록 해제 (무조건 실행됨)
		// reg가 default(비어있음)인 경우 DisposeAsync는 아무 작업도 하지 않고 즉시 완료됩니다.
		await reg.DisposeAsync();
	}

	public bool GetResult(short token) => _core.GetResult(token);
	public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

	public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
	{
		_core.OnCompleted(continuation, state, token, flags);
	}

	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);
}
