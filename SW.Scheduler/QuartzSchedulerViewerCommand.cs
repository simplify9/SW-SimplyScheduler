using Quartz;
using SW.PrimitiveTypes;

namespace SW.Scheduler;

/// <summary>
/// Quartz-backed implementation of <see cref="ISchedulerViewerCommand"/>.
/// Registered by <c>AddSchedulerCore</c> so it is available regardless of which
/// provider package (PgSql / SqlServer / MySql / in-memory) is used.
/// </summary>
internal sealed class QuartzSchedulerViewerCommand(ISchedulerFactory schedulerFactory) : ISchedulerViewerCommand
{
    private async Task<IScheduler> GetScheduler(CancellationToken ct)
        => await schedulerFactory.GetScheduler(ct);

    // ── GetAllJobs ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<JobSummary>> GetAllJobsAsync(CancellationToken ct = default)
    {
        var scheduler = await GetScheduler(ct);
        var groups    = await scheduler.GetJobGroupNames(ct);
        var result    = new List<JobSummary>();

        foreach (var group in groups)
        {
            var matcher = Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals(group);
            var keys    = await scheduler.GetJobKeys(matcher, ct);

            foreach (var key in keys)
            {
                var triggers = await scheduler.GetTriggersOfJob(key, ct);

                // Use the first trigger for state/timing info.
                ITrigger? first = triggers.FirstOrDefault();

                string triggerState = "None";
                DateTimeOffset? next = null;
                DateTimeOffset? prev = null;
                string? cron = null;

                if (first != null)
                {
                    var state = await scheduler.GetTriggerState(first.Key, ct);
                    triggerState = state.ToString();
                    next = first.GetNextFireTimeUtc();
                    prev = first.GetPreviousFireTimeUtc();

                    if (first is ICronTrigger cronTrigger)
                        cron = cronTrigger.CronExpressionString;
                }

                // Derive TypeName: last segment of group (e.g. "Jobs.SendCustomerEmailsJob" → "SendCustomerEmailsJob")
                var groupDot  = group.LastIndexOf('.');
                var typeName  = groupDot >= 0 ? group[(groupDot + 1)..] : group;

                result.Add(new JobSummary
                {
                    Group            = group,
                    Name             = key.Name,
                    TypeName         = typeName,
                    IsParameterized  = key.Name != JobKeyConventions.MainJobName,
                    TriggerState     = triggerState,
                    NextFireTime     = next,
                    PreviousFireTime = prev,
                    CronExpression   = cron
                });
            }
        }

        return result.OrderBy(j => j.Group).ThenBy(j => j.Name).ToList();
    }

    // ── Pause ─────────────────────────────────────────────────────────────────

    public async Task PauseAsync(string group, string name, CancellationToken ct = default)
    {
        var scheduler = await GetScheduler(ct);
        await scheduler.PauseJob(new JobKey(name, group), ct);
    }

    // ── Resume ────────────────────────────────────────────────────────────────

    public async Task ResumeAsync(string group, string name, CancellationToken ct = default)
    {
        var scheduler = await GetScheduler(ct);
        await scheduler.ResumeJob(new JobKey(name, group), ct);
    }

    // ── Reschedule ────────────────────────────────────────────────────────────

    public async Task RescheduleAsync(string group, string name, string newCronExpression, CancellationToken ct = default)
    {
        var scheduler = await GetScheduler(ct);
        var jobKey    = new JobKey(name, group);
        var triggers  = await scheduler.GetTriggersOfJob(jobKey, ct);

        // Find the first cron trigger to replace.
        var existing = triggers.OfType<ICronTrigger>().FirstOrDefault()
                    ?? triggers.FirstOrDefault();

        if (existing == null)
            throw new InvalidOperationException($"No trigger found for job {group}/{name}.");

        if (!CronExpression.IsValidExpression(newCronExpression))
            throw new ArgumentException($"Invalid cron expression: {newCronExpression}", nameof(newCronExpression));

        var newTrigger = TriggerBuilder.Create()
            .WithIdentity(existing.Key)
            .ForJob(jobKey)
            .WithCronSchedule(newCronExpression)
            .Build();

        await scheduler.RescheduleJob(existing.Key, newTrigger, ct);
    }

    // ── Unschedule ────────────────────────────────────────────────────────────

    public async Task UnscheduleAsync(string group, string name, CancellationToken ct = default)
    {
        var scheduler = await GetScheduler(ct);
        var jobKey    = new JobKey(name, group);

        if (name == JobKeyConventions.MainJobName)
        {
            // Simple job — remove triggers but keep the durable job registration.
            var triggers = await scheduler.GetTriggersOfJob(jobKey, ct);
            foreach (var t in triggers)
                await scheduler.UnscheduleJob(t.Key, ct);
        }
        else
        {
            // Parameterized job — delete the dedicated Quartz job entirely.
            await scheduler.DeleteJob(jobKey, ct);
        }
    }
}

