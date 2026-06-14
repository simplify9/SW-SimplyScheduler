using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SW.Scheduler.IntegrationTests.Infrastructure;

/// <summary>
/// Wraps an IHost so tests can use <c>await using</c> to automatically stop
/// the host and run any post-test cleanup (schema drop, database drop).
/// </summary>
public sealed class HostHandle : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly Func<Task>? _cleanup;

    public IServiceProvider Services => _host.Services;

    public HostHandle(IHost host, Func<Task>? cleanup = null)
    {
        _host    = host;
        _cleanup = cleanup;
    }

    public async ValueTask DisposeAsync()
    {
        // Stop Quartz (waits for in-flight jobs per WaitForJobsToComplete = true)
        await _host.StopAsync(TimeSpan.FromSeconds(15));
        _host.Dispose();

        if (_cleanup != null)
            await _cleanup();
    }
}
