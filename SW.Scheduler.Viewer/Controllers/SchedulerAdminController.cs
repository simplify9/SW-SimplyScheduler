using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SW.PrimitiveTypes;
using SW.Scheduler.Viewer;

namespace SW.Scheduler.Viewer.Controllers;

/// <summary>
/// MVC controller that powers the Scheduler Admin UI.
/// Full-page requests return complete views wrapped in _Layout.
/// HTMX requests (HX-Request header present) return partial views — the client
/// swaps only the #sw-content div, giving SPA-like navigation without JavaScript.
/// </summary>
[ServiceFilter(typeof(SchedulerViewerContextFilter))]
public class SchedulerAdminController : Controller
{
    private readonly ISchedulerViewerQuery   _query;
    private readonly ISchedulerViewerCommand _command;
    private readonly SchedulerViewerOptions  _options;

    public SchedulerAdminController(
        ISchedulerViewerQuery   query,
        ISchedulerViewerCommand command,
        IOptions<SchedulerViewerOptions> options)
    {
        _query   = query;
        _command = command;
        _options = options.Value;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsHtmx => Request.Headers.ContainsKey("HX-Request");


    // ── GET /  (dashboard) ────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var running = await _query.GetRunningAsync(ct);
        var recent  = await _query.GetRecentAsync(_options.DefaultPageSize, ct);

        var vm = new DashboardViewModel
        {
            Title   = _options.Title,
            Running = running,
            Recent  = recent
        };

        return IsHtmx ? PartialView("_Index", vm) : View("Index", vm);
    }

    // ── GET /running ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Running(CancellationToken ct)
    {
        var running = await _query.GetRunningAsync(ct);
        var vm      = new RunningViewModel { Title = _options.Title, Running = running };
        return IsHtmx ? PartialView("_Running", vm) : View("Running", vm);
    }

    // ── GET /history ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> History(
        [FromQuery] string? jobGroup = null,
        [FromQuery] bool?   success  = null,
        [FromQuery] int     limit    = 0,
        CancellationToken   ct       = default)
    {
        if (limit <= 0) limit = _options.DefaultPageSize;

        var executions = await _query.GetHistoryAsync(jobGroup, success, limit, ct);

        var vm = new HistoryViewModel
        {
            Title      = _options.Title,
            Executions = executions,
            JobGroup   = jobGroup,
            Success    = success,
            Limit      = limit
        };

        return IsHtmx ? PartialView("_History", vm) : View("History", vm);
    }

    // ── GET /detail/{id} ─────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Detail(string id, CancellationToken ct)
    {
        var execution = await _query.GetByFireInstanceIdAsync(id, ct);
        if (execution == null) return NotFound();

        var vm = new DetailViewModel { Title = _options.Title, Execution = execution };
        return IsHtmx ? PartialView("_Detail", vm) : View("Detail", vm);
    }

    // ── GET /jobs ─────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Jobs(CancellationToken ct)
    {
        var jobs = await _command.GetAllJobsAsync(ct);
        var vm   = new JobsViewModel { Title = _options.Title, Jobs = jobs };
        return IsHtmx ? PartialView("_Jobs", vm) : View("Jobs", vm);
    }

    // ── POST /jobs/pause ─────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PauseJob(
        [FromForm] string group,
        [FromForm] string name,
        CancellationToken ct)
    {
        try   { await _command.PauseAsync(group, name, ct); }
        catch (Exception ex) { TempData["Error"] = ex.Message; }

        return await RefreshJobRow(group, name, ct);
    }

    // ── POST /jobs/resume ────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResumeJob(
        [FromForm] string group,
        [FromForm] string name,
        CancellationToken ct)
    {
        try   { await _command.ResumeAsync(group, name, ct); }
        catch (Exception ex) { TempData["Error"] = ex.Message; }

        return await RefreshJobRow(group, name, ct);
    }

    // ── POST /jobs/unschedule ────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnscheduleJob(
        [FromForm] string group,
        [FromForm] string name,
        CancellationToken ct)
    {
        try   { await _command.UnscheduleAsync(group, name, ct); }
        catch (Exception ex) { TempData["Error"] = ex.Message; }

        return await RefreshJobRow(group, name, ct);
    }

    // ── POST /jobs/reschedule ────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RescheduleJob(
        [FromForm] string group,
        [FromForm] string name,
        [FromForm] string cronExpression,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            TempData["Error"] = "Cron expression is required.";
            return await RefreshJobRow(group, name, ct);
        }

        try   { await _command.RescheduleAsync(group, name, cronExpression, ct); }
        catch (Exception ex) { TempData["Error"] = ex.Message; }

        return await RefreshJobRow(group, name, ct);
    }

    // ── Helper: return the updated job row partial ────────────────────────────

    private async Task<IActionResult> RefreshJobRow(string group, string name, CancellationToken ct)
    {
        // Reload ALL jobs so the row partial has up-to-date state.
        // The partial renders only the single matching row via hx-swap="outerHTML".
        var jobs = await _command.GetAllJobsAsync(ct);
        var job  = jobs.FirstOrDefault(j => j.Group == group && j.Name == name);

        if (job == null)
        {
            // Job was deleted (unscheduled parameterized job) — return an empty response
            // so HTMX removes the row via outerHTML swap.
            return Content("", "text/html");
        }

        ViewBag.Error = TempData["Error"];
        return PartialView("_JobRow", new JobRowViewModel { Job = job });
    }
}
