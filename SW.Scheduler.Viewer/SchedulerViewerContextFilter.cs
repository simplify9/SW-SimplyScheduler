using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace SW.Scheduler.Viewer;

/// <summary>
/// Action filter that injects <c>BasePath</c> and <c>Title</c> into ViewBag
/// for every action in the Scheduler Admin UI area, so Razor views can build
/// correct hrefs without hard-coding the path prefix.
/// </summary>
internal sealed class SchedulerViewerContextFilter : IActionFilter
{
    private readonly SchedulerViewerOptions _options;

    public SchedulerViewerContextFilter(IOptions<SchedulerViewerOptions> options)
        => _options = options.Value;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.Controller is Controller ctrl)
        {
            ctrl.ViewBag.BasePath = _options.PathPrefix;
            ctrl.ViewBag.Title    = _options.Title;
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}

