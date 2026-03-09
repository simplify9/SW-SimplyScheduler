using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace SW.Scheduler.Viewer;

/// <summary>
/// Middleware that guards the Scheduler Admin UI routes.
/// Runs the user-supplied <see cref="SchedulerViewerOptions.AuthorizeAsync"/> delegate
/// before any request reaches the MVC controller. Responds with 401 on failure.
/// </summary>
internal sealed class SchedulerViewerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SchedulerViewerOptions _options;

    public SchedulerViewerMiddleware(RequestDelegate next, IOptions<SchedulerViewerOptions> options)
    {
        _next    = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path;

        // Only intercept requests under the configured prefix.
        if (!path.StartsWithSegments(_options.PathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        // Run the auth delegate when configured.
        if (_options.AuthorizeAsync != null)
        {
            var allowed = await _options.AuthorizeAsync(ctx);
            if (!allowed)
            {
                ctx.Response.StatusCode  = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "text/html; charset=utf-8";
                await ctx.Response.WriteAsync(UnauthorizedHtml(_options.Title));
                return;
            }
        }

        // Stamp the path prefix into Items so controllers/views can read it.
        ctx.Items[SchedulerViewerConstants.BasePathKey] = _options.PathPrefix;

        await _next(ctx);
    }

    private static string UnauthorizedHtml(string title) => $"""
        <!doctype html><html lang="en" data-theme="light"><head>
        <meta charset="utf-8"/>
        <title>Unauthorized — {title}</title>
        <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@picocss/pico@2/css/pico.min.css"/>
        </head><body>
        <main class="container" style="max-width:480px;margin-top:5rem;">
          <article>
            <header><strong>401 — Unauthorized</strong></header>
            <p>You do not have permission to access the Scheduler Admin UI.</p>
            <p>Please sign in with an account that has the required permissions.</p>
          </article>
        </main>
        </body></html>
        """;
}

internal static class SchedulerViewerConstants
{
    internal const string BasePathKey = "SchedulerViewer.BasePath";
}

