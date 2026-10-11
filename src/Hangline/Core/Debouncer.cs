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
        Task.Run(async () =>
        {
            try { await Task.Delay(delay, cts.Token).ConfigureAwait(false); }
            catch (TaskCanceledException) { return; }
            if (!cts.IsCancellationRequested) action();
        }, cts.Token);
        return new Cancel(cts);
    }

    private sealed class Cancel(CancellationTokenSource cts) : IDisposable
    {
        public void Dispose() { cts.Cancel(); cts.Dispose(); }
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

    public void FireNow()
    {
        Cancel();
        if (!_disposed) action();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cancel();
    }
}

/// <summary>
/// Runs an action at most once per interval, dropping the events in between
/// but always running once more if events arrived while it was busy — the
/// cursor poller needs this, not a debouncer.
/// </summary>
public sealed class Throttle(TimeSpan interval, Action action, IScheduler? scheduler = null) : IDisposable
{
    private readonly IScheduler _scheduler = scheduler ?? new SystemScheduler();
    private DateTimeOffset _lastRun = DateTimeOffset.MinValue;
    private bool _queued;
    private IDisposable? _pending;
    private bool _disposed;

    public void Trigger()
    {
        if (_disposed) return;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - _lastRun >= interval)
        {
            _lastRun = now;
            action();
            return;
        }
        if (_queued) return;
        _queued = true;
        TimeSpan wait = interval - (now - _lastRun);
        _pending = _scheduler.Schedule(wait, () =>
        {
            _queued = false;
            _pending = null;
            if (_disposed) return;
            _lastRun = DateTimeOffset.UtcNow;
            action();
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pending?.Dispose();
    }
}
