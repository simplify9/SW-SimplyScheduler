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

    /// <summary>
    /// Get all registered job definitions
    /// </summary>
    [HttpGet("jobs")]
    public IActionResult GetJobs()
    {
        var jobs = _scheduler.GetJobDefinitions()
            .Select(j => new
            {
                name = j.Name,
                @namespace = j.Namespace,
                hasParams = j.WithParams,
                type = j.JobType.FullName
            });

        return Ok(jobs);
    }

    /// <summary>
    /// Override the declarative schedule for CountCustomersJob
    /// </summary>
    [HttpPost("count-customers/reschedule")]
    public async Task<IActionResult> RescheduleCountCustomers([FromBody] string newCronExpression)
    {
        try
        {
            // First, unschedule the existing trigger (from [Schedule] attribute)
            await _scheduler.UnscheduleJob("count-customers-every-minute");

            // Create new schedule with different cron
            var triggerKey = await _scheduler.Schedule<CountCustomersJob>(
                cronExpression: newCronExpression,
                triggerKey: "count-customers-custom"
            );

            _logger.LogInformation($"Rescheduled CountCustomersJob with cron: {newCronExpression}");

            return Ok(new { triggerKey, cronExpression = newCronExpression });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reschedule CountCustomersJob");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Trigger CountCustomersJob immediately
    /// </summary>
    [HttpPost("count-customers/trigger")]
    public async Task<IActionResult> TriggerCountCustomersNow()
    {
        try
        {
            //await _scheduler.TriggerJobNow<CountCustomersJob>();
            return Ok(new { message = "Job triggered successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Schedule SendCustomerEmailsJob to run once at a specific time
    /// </summary>
    [HttpPost("send-emails/schedule-once")]
    public async Task<IActionResult> ScheduleEmailsOnce([FromBody] DateTime runAt)
    {
        try
        {
            var triggerKey = await _scheduler.ScheduleOnce<SendCustomerEmailsJob>(
                runAt: runAt
            );

            return Ok(new { triggerKey, scheduledFor = runAt });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Pause a scheduled job
    /// </summary>
    [HttpPost("pause/{triggerKey}")]
    public async Task<IActionResult> PauseJob(string triggerKey)
    {
        try
        {
            await _scheduler.PauseJob(triggerKey);
            return Ok(new { message = $"Job {triggerKey} paused" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Resume a paused job
    /// </summary>
    [HttpPost("resume/{triggerKey}")]
    public async Task<IActionResult> ResumeJob(string triggerKey)
    {
        try
        {
            await _scheduler.ResumeJob(triggerKey);
            return Ok(new { message = $"Job {triggerKey} resumed" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Unschedule a job
    /// </summary>
    [HttpDelete("{triggerKey}")]
    public async Task<IActionResult> UnscheduleJob(string triggerKey)
    {
        try
        {
            await _scheduler.UnscheduleJob(triggerKey);
            return Ok(new { message = $"Job {triggerKey} unscheduled" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Example: Schedule a job with user-defined cron expression
    /// </summary>
    [HttpPost("custom-schedule")]
    public async Task<IActionResult> CreateCustomSchedule(
        [FromBody] CreateScheduleRequest request)
    {
        try
        {
            string triggerKey;

            // Schedule based on job type
            switch (request.JobType.ToLower())
            {
                case "countcustomers":
                    triggerKey = await _scheduler.Schedule<CountCustomersJob>(
                        cronExpression: request.CronExpression,
                        triggerKey: request.TriggerKey
                    );
                    break;

                case "sendemails":
                    triggerKey = await _scheduler.Schedule<SendCustomerEmailsJob>(
                        cronExpression: request.CronExpression,
                        triggerKey: request.TriggerKey
                    );
                    break;

                default:
                    return BadRequest(new { error = "Unknown job type" });
            }

            return Ok(new
            {
                triggerKey,
                jobType = request.JobType,
                cronExpression = request.CronExpression,
                message = "Job scheduled successfully"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class CreateScheduleRequest
{
    public string JobType { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string? TriggerKey { get; set; }
}
