namespace RSocket.Payloads;

using System;
using System.Threading;
using System.Threading.Tasks.Sources;

public sealed class SingleResponseValueTaskSource<T> : IObserver<T>, IValueTaskSource<T>
{
	[ThreadStatic]
	private static SingleResponseValueTaskSource<T>? CACHE;

	public static SingleResponseValueTaskSource<T> Create()
	{
		var cache = Interlocked.Exchange(ref CACHE, null);
		if (cache is null)
		{
			return new SingleResponseValueTaskSource<T>();
		}

		return cache;
	}

	private ManualResetValueTaskSourceCore<T> _core = new();

	public short Version => _core.Version;

	private SingleResponseValueTaskSource()
	{
		_core.RunContinuationsAsynchronously = true;
	}

	public T GetResult(short token)
	{
		return _core.GetResult(token);
	}

	public ValueTaskSourceStatus GetStatus(short token)
	{
		return _core.GetStatus(token);
	}

	public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
	{
		_core.OnCompleted(continuation, state, token, flags);
	}

	void IObserver<T>.OnCompleted()
	{
		Reset();
	}

	void IObserver<T>.OnError(Exception error)
	{
		try
		{ 
			_core.SetException(error);
		}
		catch
		{
			// ignore if already completed
		}
	}

	void IObserver<T>.OnNext(T value)
	{
		try
		{
			_core.SetResult(value);
		}
		catch
		{
			// ignore if already completed
		}
	}

	private void Reset()
	{
		Interlocked.CompareExchange(ref CACHE, this, null);
	}
}
