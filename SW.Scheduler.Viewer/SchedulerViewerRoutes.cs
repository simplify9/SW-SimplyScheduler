using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace SW.Scheduler.Viewer;

/// <summary>
/// Registers the MVC routes for the Scheduler Admin UI.
/// Called as part of <c>app.MapSchedulerViewer()</c>.
/// </summary>
internal static class SchedulerViewerRoutes
{
    internal static void Map(IEndpointRouteBuilder endpoints, string prefix)
    {
        prefix = prefix.TrimEnd('/');

        endpoints.MapControllerRoute("SchedulerViewer_Index",   prefix,
            new { controller = "SchedulerAdmin", action = "Index" });

        endpoints.MapControllerRoute("SchedulerViewer_Running", prefix + "/Running",
            new { controller = "SchedulerAdmin", action = "Running" });

        endpoints.MapControllerRoute("SchedulerViewer_History", prefix + "/History",
            new { controller = "SchedulerAdmin", action = "History" });

        endpoints.MapControllerRoute("SchedulerViewer_Detail",  prefix + "/Detail/{id}",
            new { controller = "SchedulerAdmin", action = "Detail" });

        endpoints.MapControllerRoute("SchedulerViewer_Jobs",    prefix + "/Jobs",
            new { controller = "SchedulerAdmin", action = "Jobs" });

        // Management POST endpoints — called by HTMX from the Jobs page
        endpoints.MapControllerRoute("SchedulerViewer_Pause",       prefix + "/Jobs/Pause",
            new { controller = "SchedulerAdmin", action = "PauseJob" });

        endpoints.MapControllerRoute("SchedulerViewer_Resume",      prefix + "/Jobs/Resume",
            new { controller = "SchedulerAdmin", action = "ResumeJob" });

        endpoints.MapControllerRoute("SchedulerViewer_Unschedule",  prefix + "/Jobs/Unschedule",
            new { controller = "SchedulerAdmin", action = "UnscheduleJob" });

        endpoints.MapControllerRoute("SchedulerViewer_Reschedule",  prefix + "/Jobs/Reschedule",
            new { controller = "SchedulerAdmin", action = "RescheduleJob" });
    }
}

