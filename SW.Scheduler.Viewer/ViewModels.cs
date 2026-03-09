using SW.PrimitiveTypes;

namespace SW.Scheduler.Viewer;

public class DashboardViewModel
{
    public string Title { get; init; } = "Scheduler";
    public IReadOnlyList<JobExecution> Running { get; init; } = [];
    public IReadOnlyList<JobExecution> Recent  { get; init; } = [];

    public int SuccessCount  => Recent.Count(e => e.Success == true);
    public int FailureCount  => Recent.Count(e => e.Success == false);
    public int SuccessRate   => Recent.Count == 0 ? 0 : (int)(SuccessCount * 100.0 / Recent.Count);
}

public class RunningViewModel
{
    public string Title { get; init; } = "Scheduler";
    public IReadOnlyList<JobExecution> Running { get; init; } = [];
}

public class HistoryViewModel
{
    public string Title      { get; init; } = "Scheduler";
    public string? JobGroup  { get; init; }
    public bool?   Success   { get; init; }
    public int     Limit     { get; init; } = 50;
    public IReadOnlyList<JobExecution> Executions { get; init; } = [];
}

public class DetailViewModel
{
    public string Title { get; init; } = "Scheduler";
    public JobExecution Execution { get; init; } = null!;
}

public class JobsViewModel
{
    public string Title { get; init; } = "Scheduler";
    public IReadOnlyList<JobSummary> Jobs { get; init; } = [];
}

public class JobRowViewModel
{
    public JobSummary Job { get; init; } = null!;
    public string? Error  { get; init; }
}

