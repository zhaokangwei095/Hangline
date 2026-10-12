namespace Hangline.Core;

/// <summary>Postpone work until it settles. Injectable so tests need no real clock.</summary>
public interface IScheduler
{
    IDisposable Schedule(TimeSpan delay, Action action);
}

/// <summary>Wall-clock scheduler on the thread pool.</summary>
public sealed class SystemScheduler : IScheduler
{
    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var cts = new CancellationTokenSource();
        // Take the token now: the handle may be cancelled and disposed by the
        // caller at any moment, and a task that reads cts.Token afterwards would
        // throw ObjectDisposedException on the thread pool.
        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(delay, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            if (!token.IsCancellationRequested) action();
        }, token);
        return new Cancel(cts);
    }

    private sealed class Cancel(CancellationTokenSource cts) : IDisposable
    {
        public void Dispose()
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
            cts.Dispose();
        }
    }
}

/// <summary>
/// Coalesces a burst into one run: the watchers rescan 0.2 s after the last
/// filesystem event, because a screenshot is written as a temporary file and
/// then renamed.
/// </summary>
public sealed class Debouncer(TimeSpan delay, Action action, IScheduler? scheduler = null) : IDisposable
{
    private readonly IScheduler _scheduler = scheduler ?? new SystemScheduler();
    private IDisposable? _pending;
    private bool _disposed;

    public bool IsPending => _pending is not null;

    /// Restart the countdown; a burst of events fires once.
    public void Trigger()
    {
        if (_disposed) return;
        _pending?.Dispose();
        _pending = _scheduler.Schedule(delay, () =>
        {
            _pending = null;
            if (!_disposed) action();
        });
    }

    public void Cancel()
    {
        _pending?.Dispose();
        _pending = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cancel();
    }
}
