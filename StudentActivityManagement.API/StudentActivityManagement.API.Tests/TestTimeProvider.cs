namespace StudentActivityManagement.API.Tests;

public sealed class TestTimeProvider : TimeProvider
{
    private long _offsetTicks;

    public override DateTimeOffset GetUtcNow() =>
        DateTimeOffset.UtcNow.AddTicks(Interlocked.Read(ref _offsetTicks));

    public void Advance(TimeSpan by) => Interlocked.Add(ref _offsetTicks, by.Ticks);

    public IDisposable At(DateTime utc)
    {
        var previous = Interlocked.Exchange(ref _offsetTicks, (utc - DateTime.UtcNow).Ticks);
        return new ClockScope(() => Interlocked.Exchange(ref _offsetTicks, previous));
    }

    private sealed class ClockScope(Action reset) : IDisposable
    {
        public void Dispose() => reset();
    }
}
