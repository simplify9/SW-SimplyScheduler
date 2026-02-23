using Microsoft.AspNetCore.Mvc;
using SampleApplication.Jobs;
using SW.PrimitiveTypes;

namespace SampleApplication.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchedulerController : ControllerBase
{
    private readonly IScheduleRepository _scheduler;
    private readonly ILogger<SchedulerController> _logger;

    public SchedulerController(IScheduleRepository scheduler, ILogger<SchedulerController> logger)
    {
        _scheduler = scheduler;
        _logger = logger;
    }

    // =========================================================================
    // Discovery
    // =========================================================================

    /// <summary>Returns all registered job definitions.</summary>
    [HttpGet("jobs")]
    public IActionResult GetJobs()
    {
        var jobs = _scheduler.GetJobDefinitions()
            .Select(j => new
            {
                name       = j.Name,
                group      = j.Group,
                hasParams  = j.WithParams,
                paramType  = j.JobParamsType?.Name,
                type       = j.JobType.FullName
            });

        return Ok(jobs);
    }

    // =========================================================================
    // CountCustomersJob  (IScheduledJob — simple, attribute-scheduled)
    // =========================================================================

    /// <summary>
    /// Override the cron expression for CountCustomersJob at runtime.
    /// Replaces the single default trigger set by [Schedule].
    /// </summary>
    [HttpPost("count-customers/reschedule")]
    public async Task<IActionResult> RescheduleCountCustomers([FromBody] RescheduleRequest request)
    {
        try
        {
            await _scheduler.RescheduleJob<CountCustomersJob>(request.CronExpression);
            _logger.LogInformation("Rescheduled CountCustomersJob → {Cron}", request.CronExpression);
            return Ok(new { cronExpression = request.CronExpression });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reschedule CountCustomersJob");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Pause the default trigger of CountCustomersJob.</summary>
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

    /// <summary>Resume the default trigger of CountCustomersJob.</summary>
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

    /// <summary>Remove the default trigger of CountCustomersJob.</summary>
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
    // SendCustomerEmailsJob  (IScheduledJob<SendEmailsParams> — parameterized)
    // =========================================================================

    /// <summary>
    /// Create a recurring email campaign on a cron schedule.
    /// Each campaign gets its own scheduleKey so multiple can coexist.
    /// </summary>
    [HttpPost("send-emails/schedule")]
    public async Task<IActionResult> ScheduleEmailCampaign([FromBody] ScheduleEmailCampaignRequest request)
    {
        try
        {
            var @params = new SendEmailsParams(
                Subject: request.Subject,
                Body: request.Body,
                FilterByDomain: request.FilterByDomain);

            await _scheduler.Schedule<SendCustomerEmailsJob, SendEmailsParams>(
                param:         @params,
                cronExpression: request.CronExpression,
                scheduleKey:   request.ScheduleKey,
                config:        request.Config);

            _logger.LogInformation("Scheduled email campaign '{Key}' → {Cron}", request.ScheduleKey, request.CronExpression);
            return Ok(new { scheduleKey = request.ScheduleKey, cronExpression = request.CronExpression });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule email campaign");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Send a one-off email batch immediately (or at a specific time).
    /// Returns the auto-generated scheduleKey.
    /// </summary>
    [HttpPost("send-emails/schedule-once")]
    public async Task<IActionResult> ScheduleEmailsOnce([FromBody] ScheduleEmailOnceRequest request)
    {
        try
        {
            var @params = new SendEmailsParams(
                Subject: request.Subject,
                Body: request.Body,
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

    /// <summary>Pause an email campaign by its scheduleKey.</summary>
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

    /// <summary>Cancel an email campaign permanently.</summary>
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
    // =========================================================================

    /// <summary>
    /// Schedule a recurring notification for a specific customer.
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
                param:         @params,
                cronExpression: request.CronExpression,
                scheduleKey:   request.ScheduleKey);

            return Ok(new { scheduleKey = request.ScheduleKey, cronExpression = request.CronExpression });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Send a one-off notification to a customer immediately or at a given time.
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

    /// <summary>Resume a paused customer notification schedule.</summary>
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

    /// <summary>Remove a customer notification schedule.</summary>
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

// =========================================================================
// Request DTOs
// =========================================================================

public record RescheduleRequest(string CronExpression);

public record ScheduleEmailCampaignRequest(
    string ScheduleKey,
    string CronExpression,
    string Subject,
    string Body,
    string? FilterByDomain = null,
    ScheduleConfig? Config = null);

public record ScheduleEmailOnceRequest(
    string Subject,
    string Body,
    string? FilterByDomain = null,
    DateTime? RunAt = null);

public record ScheduleNotificationRequest(
    string ScheduleKey,
    string CronExpression,
    int CustomerId,
    string Channel,
    string Message);

public record NotifyCustomerOnceRequest(
    int CustomerId,
    string Channel,
    string Message,
    DateTime? RunAt = null);
