#nullable enable
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using SW.PrimitiveTypes;

namespace SW.Scheduler.Monitoring;

/// <summary>
/// Quartz <see cref="IJobListener"/> that automatically records a <see cref="JobExecution"/>
/// row for every job that fires. Works with any DbContext that includes the <c>JobExecutions</c>
/// table via <c>modelBuilder.ApplyScheduling()</c>.
/// </summary>
internal sealed class JobExecutionListener(
    IServiceProvider serviceProvider,
    SchedulerOptions options,
    ILogger<JobExecutionListener> logger) : IJobListener
{
    // ────────────────────────────────────────────────────────────────────────────
    // IJobListener identity
    // ────────────────────────────────────────────────────────────────────────────

    public string Name => "SW.Scheduler.JobExecutionListener";

    // ────────────────────────────────────────────────────────────────────────────
    // JobToBeExecuted — insert a "started" row
    // ────────────────────────────────────────────────────────────────────────────

    public async Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var record = BuildStartRecord(context);

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetService<IJobExecutionStore>();
            if (db == null) return; // monitoring not wired — skip silently

            await db.InsertAsync(record, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[JobListener] Failed to record job start for {JobKey}", context.JobDetail.Key);
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // JobWasExecuted — update the row with outcome + optionally archive to cloud
    // ────────────────────────────────────────────────────────────────────────────

    public async Task JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endTime  = DateTime.UtcNow;
            var duration = (long)(endTime - context.FireTimeUtc.UtcDateTime).TotalMilliseconds;
            var error    = jobException?.InnerException?.Message ?? jobException?.Message;

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetService<IJobExecutionStore>();
            if (db == null) return;

            await db.UpdateAsync(context.FireInstanceId, endTime, duration, error == null, error, cancellationToken);

            // ── Cloud archive (fire-and-forget; never block the job) ──────────
            if (options.EnableArchive)
                _ = ArchiveAsync(scope.ServiceProvider, context, endTime, duration, error);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[JobListener] Failed to record job completion for {JobKey}", context.JobDetail.Key);
        }
    }

    // JobExecutionVetoed is a no-op
    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    // ────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────────

    private static JobExecution BuildStartRecord(IJobExecutionContext context)
    {
        var jobKey = context.JobDetail.Key;

        // Derive the user-facing type name from the group convention ("Jobs.SendCustomerEmailsJob")
        var group    = jobKey.Group;
        var dot      = group.LastIndexOf('.');
        var typeName = dot >= 0 ? group[(dot + 1)..] : group;

        var record = new JobExecution
        {
            JobName        = jobKey.Name,
            JobGroup       = group,
            JobTypeName    = typeName,
            FireInstanceId = context.FireInstanceId,
            StartTimeUtc   = context.FireTimeUtc.UtcDateTime,
            Node           = Environment.MachineName
        };

        // Capture the job parameter (if any) into the Context column.
        // The raw JSON string stored in the data map is deserialized into a
        // plain object so System.Text.Json re-serializes it as a JSON value
        // (not a double-encoded string) inside the ScheduledJobContext envelope.
        context.MergedJobDataMap.TryGetString(Constants.JobParamsKey, out var rawParams);
        if (!string.IsNullOrWhiteSpace(rawParams))
        {
            var paramObject = JsonSerializer.Deserialize<object>(rawParams, _serializerOptions);
            record.SetContext(new ScheduledJobContext { JobParameter = paramObject });
        }

        return record;
    }

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private async Task ArchiveAsync(
        IServiceProvider scopedProvider,
        IJobExecutionContext context,
        DateTime endTime,
        long durationMs,
        string? error)
    {
        try
        {
            var cloudFiles = scopedProvider.GetService<ICloudFilesService>();
            if (cloudFiles == null)
            {
                logger.LogDebug("[JobListener] Archive enabled but ICloudFilesService is not registered. Skipping.");
                return;
            }

            var jobKey  = context.JobDetail.Key;
            var now     = endTime;
            var key     = $"{options.CloudFilesPrefix}job-history/{jobKey.Group}/{now:yyyy}/{now:MM}/{now:dd}/{context.FireInstanceId}.json";

            var payload = new
            {
                jobName        = jobKey.Name,
                jobGroup       = jobKey.Group,
                fireInstanceId = context.FireInstanceId,
                startTimeUtc   = context.FireTimeUtc.UtcDateTime,
                endTimeUtc     = endTime,
                durationMs,
                success        = error == null,
                error,
                node           = Environment.MachineName
            };

            var json  = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            using var stream = new MemoryStream(bytes);
            await cloudFiles.WriteAsync(stream, new WriteFileSettings
            {
                Key         = key,
                ContentType = "application/json",
                Public      = false
            });

            logger.LogDebug("[JobListener] Archived execution {FireInstanceId} → {Key}", context.FireInstanceId, key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[JobListener] Failed to archive execution {FireInstanceId} to cloud.", context.FireInstanceId);
        }
    }
}



