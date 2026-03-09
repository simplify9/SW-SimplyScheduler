using Microsoft.EntityFrameworkCore;
using SW.Scheduler;

namespace SW.Scheduler.EfCore;

/// <summary>
/// EF Core implementation of <see cref="ISchedulerViewerQuery"/>.
/// Registered by <c>AddSchedulerMonitoring&lt;TDbContext&gt;()</c> so the
/// Scheduler Admin UI (SW.Scheduler.Viewer) can query execution history
/// without needing to know concrete job types at compile time.
/// </summary>
internal sealed class EfCoreSchedulerViewerQuery<TDbContext> : ISchedulerViewerQuery
    where TDbContext : DbContext
{
    private readonly TDbContext _db;

    public EfCoreSchedulerViewerQuery(TDbContext db) => _db = db;

    private IQueryable<JobExecution> Set => _db.Set<JobExecution>();

    public async Task<IReadOnlyList<JobExecution>> GetRunningAsync(CancellationToken ct = default)
        => await Set
            .Where(e => e.EndTimeUtc == null)
            .OrderByDescending(e => e.StartTimeUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<JobExecution>> GetRecentAsync(int limit, CancellationToken ct = default)
        => await Set
            .OrderByDescending(e => e.StartTimeUtc)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<JobExecution>> GetHistoryAsync(
        string? jobGroup,
        bool?   success,
        int     limit,
        CancellationToken ct = default)
    {
        var query = Set.AsQueryable();

        if (!string.IsNullOrWhiteSpace(jobGroup))
            query = query.Where(e => e.JobGroup.Contains(jobGroup) || e.JobTypeName.Contains(jobGroup));

        if (success.HasValue)
            query = query.Where(e => e.Success == success.Value);

        return await query
            .OrderByDescending(e => e.StartTimeUtc)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<JobExecution?> GetByFireInstanceIdAsync(string fireInstanceId, CancellationToken ct = default)
        => await Set.FirstOrDefaultAsync(e => e.FireInstanceId == fireInstanceId, ct);
}

