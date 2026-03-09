using Microsoft.AspNetCore.Mvc;
using SampleApplication.Jobs;
using SW.PrimitiveTypes;

namespace SampleApplication.Controllers;

/// <summary>
/// Demonstrates the IScheduleReader dashboard API — read-only execution history.
///
/// Covers:
///   - GetRunningExecutions()       — all jobs currently in-flight across cluster nodes
///   - GetLastExecution             — simple and parameterized overloads
///   - GetRecentExecutions          — simple and parameterized overloads
///   - GetFailedExecutions          — simple and parameterized overloads
///
/// History is kept for RetentionDays (default 30) then deleted by the cleanup job.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DashboardController(IScheduleReader reader) : ControllerBase
{
    // =========================================================================
    // Running executions (all jobs, all nodes)
    // =========================================================================

    /// <summary>Returns all job executions currently in progress across all jobs and nodes.</summary>
    [HttpGet("running")]
    public async Task<IActionResult> GetRunning()
    {
        try { return Ok(await reader.GetRunningExecutions()); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // =========================================================================
    // CountCustomersJob  (simple job)
    // =========================================================================

    /// <summary>Most recent execution of CountCustomersJob.</summary>
    [HttpGet("count-customers/last")]
    public async Task<IActionResult> GetCountCustomersLast()
    {
        try
        {
            var result = await reader.GetLastExecution<CountCustomersJob>();
            return result == null ? NotFound(new { message = "No executions recorded yet." }) : Ok(result);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>Most recent N executions of CountCustomersJob, newest first.</summary>
    [HttpGet("count-customers/recent")]
    public async Task<IActionResult> GetCountCustomersRecent([FromQuery] int limit = 20)
    {
        try { return Ok(await reader.GetRecentExecutions<CountCustomersJob>(limit)); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>All failed executions of CountCustomersJob since a given UTC date.</summary>
    [HttpGet("count-customers/failed")]
    public async Task<IActionResult> GetCountCustomersFailed([FromQuery] DateTime? since = null)
    {
        try { return Ok(await reader.GetFailedExecutions<CountCustomersJob>(since)); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // =========================================================================
    // GenerateReportJob  (simple job — no retry, MisfireInstructions.Skip)
    // =========================================================================

    /// <summary>Most recent execution of GenerateReportJob.</summary>
    [HttpGet("generate-report/last")]
    public async Task<IActionResult> GetGenerateReportLast()
    {
        try
        {
            var result = await reader.GetLastExecution<GenerateReportJob>();
            return result == null ? NotFound(new { message = "No executions recorded yet." }) : Ok(result);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>Most recent N executions of GenerateReportJob, newest first.</summary>
    [HttpGet("generate-report/recent")]
    public async Task<IActionResult> GetGenerateReportRecent([FromQuery] int limit = 20)
    {
        try { return Ok(await reader.GetRecentExecutions<GenerateReportJob>(limit)); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>All failed executions of GenerateReportJob since a given UTC date.</summary>
    [HttpGet("generate-report/failed")]
    public async Task<IActionResult> GetGenerateReportFailed([FromQuery] DateTime? since = null)
    {
        try { return Ok(await reader.GetFailedExecutions<GenerateReportJob>(since)); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // =========================================================================
    // SendCustomerEmailsJob  (parameterized — identified by scheduleKey)
    // =========================================================================

    /// <summary>Most recent execution of a SendCustomerEmailsJob schedule.</summary>
    [HttpGet("send-emails/{scheduleKey}/last")]
    public async Task<IActionResult> GetEmailJobLast(string scheduleKey)
    {
        try
        {
            var result = await reader.GetLastExecution<SendCustomerEmailsJob, SendEmailsParams>(scheduleKey);
            return result == null ? NotFound(new { message = $"No executions found for schedule '{scheduleKey}'." }) : Ok(result);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>Most recent N executions of a SendCustomerEmailsJob schedule, newest first.</summary>
    [HttpGet("send-emails/{scheduleKey}/recent")]
    public async Task<IActionResult> GetEmailJobRecent(string scheduleKey, [FromQuery] int limit = 20)
    {
        try { return Ok(await reader.GetRecentExecutions<SendCustomerEmailsJob, SendEmailsParams>(scheduleKey, limit)); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>All failed executions of a SendCustomerEmailsJob schedule since a given UTC date.</summary>
    [HttpGet("send-emails/{scheduleKey}/failed")]
    public async Task<IActionResult> GetEmailJobFailed(string scheduleKey, [FromQuery] DateTime? since = null)
    {
        try { return Ok(await reader.GetFailedExecutions<SendCustomerEmailsJob, SendEmailsParams>(scheduleKey, since)); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // =========================================================================
    // NotifyCustomerJob  (parameterized — identified by scheduleKey)
    // =========================================================================

    /// <summary>Most recent execution of a NotifyCustomerJob schedule.</summary>
    [HttpGet("notify-customer/{scheduleKey}/last")]
    public async Task<IActionResult> GetNotifyJobLast(string scheduleKey)
    {
        try
        {
            var result = await reader.GetLastExecution<NotifyCustomerJob, NotifyCustomerParams>(scheduleKey);
            return result == null ? NotFound(new { message = $"No executions found for schedule '{scheduleKey}'." }) : Ok(result);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>Most recent N executions of a NotifyCustomerJob schedule, newest first.</summary>
    [HttpGet("notify-customer/{scheduleKey}/recent")]
    public async Task<IActionResult> GetNotifyJobRecent(string scheduleKey, [FromQuery] int limit = 20)
    {
        try { return Ok(await reader.GetRecentExecutions<NotifyCustomerJob, NotifyCustomerParams>(scheduleKey, limit)); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>All failed executions of a NotifyCustomerJob schedule since a given UTC date.</summary>
    [HttpGet("notify-customer/{scheduleKey}/failed")]
    public async Task<IActionResult> GetNotifyJobFailed(string scheduleKey, [FromQuery] DateTime? since = null)
    {
        try { return Ok(await reader.GetFailedExecutions<NotifyCustomerJob, NotifyCustomerParams>(scheduleKey, since)); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }
}
