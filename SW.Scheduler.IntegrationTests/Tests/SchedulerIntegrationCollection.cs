using Xunit;

namespace SW.Scheduler.IntegrationTests.Tests;

/// <summary>
/// Serialises all three provider test classes so they run one at a time.
/// This prevents the static TestJobSink from receiving results from
/// concurrent test runs across different providers.
/// </summary>
[CollectionDefinition("SchedulerIntegration", DisableParallelization = true)]
public class SchedulerIntegrationCollection { }
