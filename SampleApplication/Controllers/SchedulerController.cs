using Microsoft.AspNetCore.Mvc;
using SampleApplication.Jobs;
using SW.PrimitiveTypes;

namespace SampleApplication.Controllers;

/// <summary>
/// Demonstrates the full IScheduleRepository API:
///   - Simple jobs  (IScheduledJob)       → no scheduleKey, single default trigger
///   - Parameterized jobs (IScheduledJob&lt;TParam&gt;) → scheduleKey required, independent Quartz job per key
///   - Schedule, ScheduleOnce, Reschedule, Pause, Resume, Unschedule
///   - Per-schedule ScheduleConfig override (concurrency, misfire, retry)
///   - Job discovery (GetJobDefinitions)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SchedulerController : ControllerBase
{
    private readonly IScheduleRepository _scheduler;
    private readonly ILogger<SchedulerController> _logger;

    public SchedulerController(IScheduleRepository scheduler, ILogger<SchedulerController> logger)
    {
        _scheduler = scheduler;
        _logger    = logger;
    }

    // =========================================================================
    // Discovery
    // =========================================================================

    /// <summary>
    /// Returns metadata for all job types discovered and registered at startup.
    /// Shows job name, group, whether it takes params, and the concrete type.
    /// </summary>
    [HttpGet("jobs")]
    public IActionResult GetJobs()
    {
        var jobs = _scheduler.GetJobDefinitions()
            .Select(j => new
            {
                name      = j.Name,
                group     = j.Group,
                hasParams = j.WithParams,
                paramType = j.JobParamsType?.Name,
                type      = j.JobType.FullName
            });
        return Ok(jobs);
    }

    // =========================================================================
    // CountCustomersJob  (IScheduledJob — simple, attribute-scheduled)
    //   [Schedule("0 * * * * ?")]  → runs every minute by default
    //   [RetryConfig(MaxRetries = 3, RetryAfterMinutes = 2)]
    //   [ScheduleConfig(AllowConcurrentExecution = false)]
    // =========================================================================

    /// <summary>
    /// Override CountCustomersJob's cron schedule at runtime.
    /// Replaces the trigger originally set by the [Schedule] attribute.
    /// </summary>
    [HttpPost("count-customers/schedule")]
    public async Task<IActionResult> ScheduleCountCustomers([FromBody] RescheduleRequest request)
    {
        try
        {
            await _scheduler.Schedule<CountCustomersJob>(request.CronExpression);
            _logger.LogInformation("Scheduled CountCustomersJob → {Cron}", request.CronExpression);
            return Ok(new { cronExpression = request.CronExpression });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Reschedule CountCustomersJob to a new cron expression.</summary>
    [HttpPost("count-customers/reschedule")]
    public async Task<IActionResult> RescheduleCountCustomers([FromBody] RescheduleRequest request)
    {
        try
        {
            await _scheduler.RescheduleJob<CountCustomersJob>(request.CronExpression);
            _logger.LogInformation("Rescheduled CountCustomersJob → {Cron}", request.CronExpression);
            return Ok(new { cronExpression = request.CronExpression });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Pause CountCustomersJob's default trigger.</summary>
    [HttpPost("count-customers/pause")]
    public async Task<IActionResult> PauseCountCustomers()
    {
        try
        {
            await _scheduler.PauseJob<CountCustomersJob>();
            return Ok(new { message = "CountCustomersJob paused" });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Resume CountCustomersJob's default trigger.</summary>
    [HttpPost("count-customers/resume")]
    public async Task<IActionResult> ResumeCountCustomers()
    {
        try
        {
            await _scheduler.ResumeJob<CountCustomersJob>();
            return Ok(new { message = "CountCustomersJob resumed" });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Remove CountCustomersJob's default trigger (job stays registered).</summary>
    [HttpDelete("count-customers")]
    public async Task<IActionResult> UnscheduleCountCustomers()
    {
        try
        {
            await _scheduler.UnscheduleJob<CountCustomersJob>();
            return Ok(new { message = "CountCustomersJob unscheduled" });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // =========================================================================
    // GenerateReportJob  (IScheduledJob — simple, attribute-scheduled)
    //   [Schedule("0 0 6 * * ?")]  → daily at 6 AM by default
    //   [ScheduleConfig(MisfireInstructions = MisfireInstructions.Skip)]
    //   No [RetryConfig] — failures are skipped
    // =========================================================================

    /// <summary>
    /// Override GenerateReportJob's cron schedule at runtime.
    /// Shows how runtime scheduling replaces the attribute-defined trigger.
    /// </summary>
    [HttpPost("generate-report/schedule")]
    public async Task<IActionResult> ScheduleGenerateReport([FromBody] RescheduleRequest request)
    {
        try
        {
            await _scheduler.Schedule<GenerateReportJob>(request.CronExpression);
            return Ok(new { cronExpression = request.CronExpression });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Reschedule GenerateReportJob to a new cron expression.</summary>
    [HttpPost("generate-report/reschedule")]
    public async Task<IActionResult> RescheduleGenerateReport([FromBody] RescheduleRequest request)
    {
        try
        {
            await _scheduler.RescheduleJob<GenerateReportJob>(request.CronExpression);
            return Ok(new { cronExpression = request.CronExpression });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Pause GenerateReportJob.</summary>
    [HttpPost("generate-report/pause")]
    public async Task<IActionResult> PauseGenerateReport()
    {
        try
        {
            await _scheduler.PauseJob<GenerateReportJob>();
            return Ok(new { message = "GenerateReportJob paused" });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Resume GenerateReportJob.</summary>
    [HttpPost("generate-report/resume")]
    public async Task<IActionResult> ResumeGenerateReport()
    {
        try
        {
            await _scheduler.ResumeJob<GenerateReportJob>();
            return Ok(new { message = "GenerateReportJob resumed" });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Unschedule GenerateReportJob.</summary>
    [HttpDelete("generate-report")]
    public async Task<IActionResult> UnscheduleGenerateReport()
    {
        try
        {
            await _scheduler.UnscheduleJob<GenerateReportJob>();
            return Ok(new { message = "GenerateReportJob unscheduled" });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // =========================================================================
    // SendCustomerEmailsJob  (IScheduledJob<SendEmailsParams> — parameterized)
    //   [ScheduleConfig(AllowConcurrentExecution = true)]  → campaigns can run in parallel
    //   [RetryConfig(MaxRetries = 5, RetryAfterMinutes = 10)]
    // =========================================================================

    /// <summary>
    /// Create a recurring email campaign.
    /// Each campaign has its own scheduleKey so multiple can coexist.
    /// Demonstrates passing a ScheduleConfig override at scheduling time.
    /// </summary>
    [HttpPost("send-emails/schedule")]
    public async Task<IActionResult> ScheduleEmailCampaign([FromBody] ScheduleEmailCampaignRequest request)
    {
        try
        {
            var @params = new SendEmailsParams(
                Subject:        request.Subject,
                Body:           request.Body,
                FilterByDomain: request.FilterByDomain);

            await _scheduler.Schedule<SendCustomerEmailsJob, SendEmailsParams>(
                param:          @params,
                cronExpression: request.CronExpression,
                scheduleKey:    request.ScheduleKey,
                config:         request.Config);

            _logger.LogInformation("Scheduled email campaign '{Key}' → {Cron}", request.ScheduleKey, request.CronExpression);
            return Ok(new { scheduleKey = request.ScheduleKey, cronExpression = request.CronExpression });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Send a one-off email batch immediately or at a specific UTC time.
    /// Demonstrates ScheduleOnce — returns the auto-generated scheduleKey.
    /// </summary>
    [HttpPost("send-emails/schedule-once")]
    public async Task<IActionResult> ScheduleEmailsOnce([FromBody] ScheduleEmailOnceRequest request)
    {
        try
        {
            var @params = new SendEmailsParams(
                Subject:        request.Subject,
                Body:           request.Body,
                FilterByDomain: request.FilterByDomain);

            var scheduleKey = await _scheduler.ScheduleOnce<SendCustomerEmailsJob, SendEmailsParams>(
                param:  @params,
                runAt:  request.RunAt);

            return Ok(new { scheduleKey, scheduledFor = request.RunAt ?? DateTime.UtcNow });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Reschedule an existing email campaign to a new cron expression.</summary>
    [HttpPost("send-emails/{scheduleKey}/reschedule")]
    public async Task<IActionResult> RescheduleEmailCampaign(string scheduleKey, [FromBody] RescheduleRequest request)
    {
        try
        {
            await _scheduler.RescheduleJob<SendCustomerEmailsJob, SendEmailsParams>(scheduleKey, request.CronExpression);
            return Ok(new { scheduleKey, cronExpression = request.CronExpression });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Pause an email campaign.</summary>
    [HttpPost("send-emails/{scheduleKey}/pause")]
    public async Task<IActionResult> PauseEmailCampaign(string scheduleKey)
    {
        try
        {
            await _scheduler.PauseJob<SendCustomerEmailsJob, SendEmailsParams>(scheduleKey);
            return Ok(new { message = $"Campaign '{scheduleKey}' paused" });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Resume a paused email campaign.</summary>
    [HttpPost("send-emails/{scheduleKey}/resume")]
    public async Task<IActionResult> ResumeEmailCampaign(string scheduleKey)
    {
        try
        {
            await _scheduler.ResumeJob<SendCustomerEmailsJob, SendEmailsParams>(scheduleKey);
            return Ok(new { message = $"Campaign '{scheduleKey}' resumed" });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Permanently cancel an email campaign and remove its Quartz job.</summary>
    [HttpDelete("send-emails/{scheduleKey}")]
    public async Task<IActionResult> UnscheduleEmailCampaign(string scheduleKey)
    {
        try
        {
            await _scheduler.UnscheduleJob<SendCustomerEmailsJob, SendEmailsParams>(scheduleKey);
            return Ok(new { message = $"Campaign '{scheduleKey}' removed" });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // =========================================================================
    // NotifyCustomerJob  (IScheduledJob<NotifyCustomerParams> — parameterized)
    //   [ScheduleConfig(AllowConcurrentExecution = false, MisfireInstructions = Skip)]
    // =========================================================================

    /// <summary>
    /// Schedule a recurring notification for a customer on a cron.
    /// Demonstrates a per-schedule ScheduleConfig with retry override.
    /// </summary>
    [HttpPost("notify-customer/schedule")]
    public async Task<IActionResult> ScheduleNotification([FromBody] ScheduleNotificationRequest request)
    {
        try
        {
            var @params = new NotifyCustomerParams(
                CustomerId: request.CustomerId,
                Channel:    request.Channel,
                Message:    request.Message);

            await _scheduler.Schedule<NotifyCustomerJob, NotifyCustomerParams>(
                param:          @params,
                cronExpression: request.CronExpression,
                scheduleKey:    request.ScheduleKey,
                config:         request.Config);

            return Ok(new { scheduleKey = request.ScheduleKey, cronExpression = request.CronExpression });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Send a one-off notification to a customer immediately or at a given time.
    /// Demonstrates ScheduleOnce for a parameterized job.
    /// </summary>
    [HttpPost("notify-customer/schedule-once")]
    public async Task<IActionResult> NotifyCustomerOnce([FromBody] NotifyCustomerOnceRequest request)
    {
        try
        {
            var @params = new NotifyCustomerParams(
                CustomerId: request.CustomerId,
                Channel:    request.Channel,
                Message:    request.Message);

            var scheduleKey = await _scheduler.ScheduleOnce<NotifyCustomerJob, NotifyCustomerParams>(
                param:  @params,
                runAt:  request.RunAt);

            return Ok(new { scheduleKey, scheduledFor = request.RunAt ?? DateTime.UtcNow });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Reschedule a customer notification to a new cron expression.</summary>
    [HttpPost("notify-customer/{scheduleKey}/reschedule")]
    public async Task<IActionResult> RescheduleNotification(string scheduleKey, [FromBody] RescheduleRequest request)
    {
        try
        {
            await _scheduler.RescheduleJob<NotifyCustomerJob, NotifyCustomerParams>(scheduleKey, request.CronExpression);
            return Ok(new { scheduleKey, cronExpression = request.CronExpression });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Pause a customer notification schedule.</summary>
    [HttpPost("notify-customer/{scheduleKey}/pause")]
    public async Task<IActionResult> PauseNotification(string scheduleKey)
    {
        try
        {
            await _scheduler.PauseJob<NotifyCustomerJob, NotifyCustomerParams>(scheduleKey);
            return Ok(new { message = $"Notification '{scheduleKey}' paused" });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Resume a paused notification schedule.</summary>
    [HttpPost("notify-customer/{scheduleKey}/resume")]
    public async Task<IActionResult> ResumeNotification(string scheduleKey)
    {
        try
        {
            await _scheduler.ResumeJob<NotifyCustomerJob, NotifyCustomerParams>(scheduleKey);
            return Ok(new { message = $"Notification '{scheduleKey}' resumed" });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Remove a customer notification schedule permanently.</summary>
    [HttpDelete("notify-customer/{scheduleKey}")]
    public async Task<IActionResult> UnscheduleNotification(string scheduleKey)
    {
        try
        {
            await _scheduler.UnscheduleJob<NotifyCustomerJob, NotifyCustomerParams>(scheduleKey);
            return Ok(new { message = $"Notification '{scheduleKey}' removed" });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Request DTOs
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Used for all simple-job cron override/reschedule endpoints.</summary>
public record RescheduleRequest(string CronExpression);

/// <summary>Create a recurring email campaign with optional per-schedule config override.</summary>
public record ScheduleEmailCampaignRequest(
    string ScheduleKey,
    string CronExpression,
    string Subject,
    string Body,
    string? FilterByDomain = null,
    ScheduleConfig? Config = null);

/// <summary>Run an email batch once, optionally at a specified UTC time.</summary>
public record ScheduleEmailOnceRequest(
    string Subject,
    string Body,
    string? FilterByDomain = null,
    DateTime? RunAt = null);

/// <summary>Create a recurring customer notification with optional per-schedule config override.</summary>
public record ScheduleNotificationRequest(
    string ScheduleKey,
    string CronExpression,
    int CustomerId,
    string Channel,
    string Message,
    ScheduleConfig? Config = null);

/// <summary>Send a one-off notification to a customer.</summary>
public record NotifyCustomerOnceRequest(
    int CustomerId,
    string Channel,
    string Message,
    DateTime? RunAt = null);
