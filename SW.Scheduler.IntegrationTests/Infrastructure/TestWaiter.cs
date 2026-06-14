namespace SW.Scheduler.IntegrationTests.Infrastructure;

public static class TestWaiter
{
    /// <summary>
    /// Polls <paramref name="condition"/> every 100 ms until it returns true or
    /// <paramref name="timeout"/> elapses. Throws <see cref="TimeoutException"/> on timeout.
    /// </summary>
    public static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string description = "condition to become true")
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            while (!condition())
                await Task.Delay(100, cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"Timed out after {timeout.TotalSeconds:F1}s waiting for: {description}");
        }
    }

    /// <summary>
    /// Convenience overload taking timeout in seconds.
    /// </summary>
    public static Task WaitUntilAsync(Func<bool> condition, int timeoutSeconds, string description = "condition")
        => WaitUntilAsync(condition, TimeSpan.FromSeconds(timeoutSeconds), description);
}
