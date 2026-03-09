using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SW.Scheduler.Viewer;

// ...existing code...

public static class SchedulerViewerServiceCollectionExtensions
{
    // ...existing code...
    public static IServiceCollection AddSchedulerViewer(
        this IServiceCollection services,
        Action<SchedulerViewerOptions>? configure = null)
    {
        var optBuilder = services.AddOptions<SchedulerViewerOptions>();
        if (configure != null) optBuilder.Configure(configure);

        // Configure antiforgery to accept token from the HTMX request header
        services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

        services.AddHttpContextAccessor();
        services.AddScoped<SchedulerViewerContextFilter>();

        // Add this assembly's controllers and Razor views to MVC.
        services
            .AddMvcCore()
            .AddApplicationPart(typeof(SchedulerViewerServiceCollectionExtensions).Assembly)
            .AddRazorViewEngine();

        return services;
    }
}

// ...existing code...

/// <summary>
/// Mounts the Scheduler Admin UI middleware and routes on the application pipeline.
/// </summary>
public static class SchedulerViewerApplicationBuilderExtensions
{
    /// <summary>
    /// Mounts the Scheduler Admin UI at the configured path prefix
    /// (default: <c>/scheduler-management</c>).
    ///
    /// Usage:
    /// <code>
    /// // Program.cs
    /// app.UseSchedulerViewer();
    ///
    /// // With options:
    /// app.UseSchedulerViewer(opts =>
    /// {
    ///     opts.PathPrefix    = "/admin/jobs";
    ///     opts.Title         = "My App Scheduler";
    ///     opts.AuthorizeAsync = ctx => Task.FromResult(ctx.User.IsInRole("Admin"));
    /// });
    /// </code>
    /// </summary>
    public static IApplicationBuilder UseSchedulerViewer(
        this IApplicationBuilder app,
        Action<SchedulerViewerOptions>? configure = null)
    {
        if (configure != null)
        {
            var opts = app.ApplicationServices
                .GetRequiredService<IOptions<SchedulerViewerOptions>>().Value;
            configure(opts);
        }

        app.UseMiddleware<SchedulerViewerMiddleware>();
        return app;
    }

    /// <summary>
    /// Registers the Scheduler Admin UI MVC area routes.
    /// Call on <see cref="IEndpointRouteBuilder"/> after <c>UseRouting()</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapSchedulerViewer(this IEndpointRouteBuilder endpoints)
    {
        var opts = endpoints.ServiceProvider
            .GetRequiredService<IOptions<SchedulerViewerOptions>>().Value;

        SchedulerViewerRoutes.Map(endpoints, opts.PathPrefix);
        return endpoints;
    }
}
