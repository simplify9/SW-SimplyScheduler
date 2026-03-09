using Microsoft.EntityFrameworkCore;
using SW.PrimitiveTypes;
using SW.Scheduler.Monitoring;

namespace SW.Scheduler.EfCore;

/// <summary>
/// EF Core implementation of <see cref="IJobExecutionStore"/> and <see cref="IJobExecutionCleanupStore"/>.
/// Uses the user's own <typeparamref name="TDbContext"/>, which must include <c>JobExecutions</c>
/// via <c>modelBuilder.ApplyScheduling()</c>.
/// </summary>
internal sealed class EfJobExecutionStore<TDbContext>(TDbContext db)
    : IJobExecutionStore, IJobExecutionCleanupStore
    where TDbContext : DbContext
{
    // ── IJobExecutionStore ────────────────────────────────────────────────────

    public async Task InsertAsync(JobExecution record, CancellationToken ct = default)
    {
        db.Set<JobExecution>().Add(record);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(
        string fireInstanceId,
        DateTime endTimeUtc,
        long durationMs,
        bool success,
        string? error,
        CancellationToken ct = default)
    {
        var row = await db.Set<JobExecution>()
            .FirstOrDefaultAsync(x => x.FireInstanceId == fireInstanceId, ct);

        if (row == null) return; // defensive — should not happen

        row.EndTimeUtc = endTimeUtc;
        row.DurationMs = durationMs;
        row.Success    = success;
        row.Error      = error;

        await db.SaveChangesAsync(ct);
    }

    public async Task<List<JobExecution>> QueryAsync(
        string jobGroup,
        string jobName,
        bool? successFilter,
        DateTime? since,
        bool runningOnly,
        int? limit,
        CancellationToken ct = default)
    {
        IQueryable<JobExecution> q = db.Set<JobExecution>().AsNoTracking();

        if (runningOnly)
        {
            q = q.Where(x => x.EndTimeUtc == null);
        }
        else
        {
            // Null group/name means "all jobs" (used by GetRunningExecutions).
            if (!string.IsNullOrEmpty(jobGroup)) q = q.Where(x => x.JobGroup == jobGroup);
            if (!string.IsNullOrEmpty(jobName))  q = q.Where(x => x.JobName  == jobName);
        }

        if (since.HasValue)
            q = q.Where(x => x.StartTimeUtc >= since.Value);

        if (successFilter.HasValue)
            q = q.Where(x => x.Success == successFilter.Value);

        q = q.OrderByDescending(x => x.StartTimeUtc);

        if (limit.HasValue)
            q = q.Take(limit.Value);

        return await q.ToListAsync(ct);
    }

    // ── IJobExecutionCleanupStore ─────────────────────────────────────────────

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        // EF Core 7+ ExecuteDeleteAsync for a single efficient DELETE statement.
        return await db.Set<JobExecution>()
            .Where(x => x.StartTimeUtc < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}


