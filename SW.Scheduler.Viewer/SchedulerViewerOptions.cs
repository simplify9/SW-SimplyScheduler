using Microsoft.AspNetCore.Http;

namespace SW.Scheduler.Viewer;

/// <summary>
/// Options for the Scheduler Admin UI middleware.
/// </summary>
public class SchedulerViewerOptions
{
    /// <summary>
    /// URL path prefix under which the admin UI is mounted.
    /// Must start with '/'. Default: <c>"/scheduler-management"</c>.
    /// </summary>
    public string PathPrefix { get; set; } = "/scheduler-management";

    /// <summary>
    /// Optional authentication delegate. Called once per request before serving any UI page.
    ///
    /// Return <c>true</c>  → allow the request.
    /// Return <c>false</c> → respond with 401 Unauthorized.
    ///
    /// When <c>null</c> (default) all requests are allowed — development only.
    ///
    /// Examples:
    /// <code>
    /// // ASP.NET Core Identity role check
    /// opts.AuthorizeAsync = ctx => Task.FromResult(ctx.User.IsInRole("Admin"));
    ///
    /// // API key header
    /// opts.AuthorizeAsync = ctx =>
    /// {
    ///     var key = ctx.Request.Headers["X-Scheduler-Key"].FirstOrDefault();
    ///     return Task.FromResult(key == configuration["Scheduler:AdminKey"]);
    /// };
    ///
    /// // Cookie / session
    /// opts.AuthorizeAsync = ctx =>
    ///     Task.FromResult(ctx.Request.Cookies["scheduler_auth"] == "my-token");
    /// </code>
    /// </summary>
    public Func<HttpContext, Task<bool>>? AuthorizeAsync { get; set; }

    /// <summary>
    /// Default number of rows returned on the history page.
    /// Default: <c>50</c>.
    /// </summary>
    public int DefaultPageSize { get; set; } = 50;

    /// <summary>
    /// Title shown in the browser tab and page header.
    /// Default: <c>"Scheduler"</c>.
    /// </summary>
    public string Title { get; set; } = "Scheduler";
}
