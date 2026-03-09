using Microsoft.EntityFrameworkCore;
using SW.Scheduler;
using SW.Scheduler.EfCore;
using SW.Scheduler.PgSql;
using SW.Scheduler.Viewer;
using SampleApplication.Data;
using SampleApplication;

var builder = WebApplication.CreateBuilder(args);

var cfg = builder.Configuration;

// ─────────────────────────────────────────────────────────────────────────────
// Read scheduler config
// Scheduler:UseDatabase = false  → in-memory Quartz + InMemory EF (default)
// Scheduler:UseDatabase = true   → PostgreSQL Quartz + Npgsql EF
// ─────────────────────────────────────────────────────────────────────────────
var useDatabase = cfg.GetValue<bool>("Scheduler:UseDatabase");
var schema      = cfg.GetValue<string>("Scheduler:Schema") ?? "quartz";

// ─────────────────────────────────────────────────────────────────────────────
// Infrastructure
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SW.Scheduler Sample", Version = "v1" });
});

// ─────────────────────────────────────────────────────────────────────────────
// EF Core
// ─────────────────────────────────────────────────────────────────────────────
if (useDatabase)
{
    var connStr = cfg.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:Postgres is required when Scheduler:UseDatabase = true");

    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseNpgsql(connStr));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseInMemoryDatabase("SampleAppDb"));
}

// ─────────────────────────────────────────────────────────────────────────────
// Scheduler options — shared regardless of provider
// ─────────────────────────────────────────────────────────────────────────────
void ConfigureSchedulerOptions(SchedulerOptions options)
{
    options.SystemUserIdentifier  = cfg.GetValue<string>("Scheduler:SystemUserIdentifier")  ?? "sample-scheduler";
    options.RetentionDays         = cfg.GetValue<int>("Scheduler:RetentionDays",         30);
    options.CleanupCronExpression = cfg.GetValue<string>("Scheduler:CleanupCronExpression") ?? "0 0 2 * * ?";
    options.EnableArchive         = cfg.GetValue<bool>("Scheduler:EnableArchive",         false);
    options.CloudFilesPrefix      = cfg.GetValue<string>("Scheduler:CloudFilesPrefix")     ?? "sample-app/";
}

// ─────────────────────────────────────────────────────────────────────────────
// Scheduler — in-memory or PostgreSQL
// ─────────────────────────────────────────────────────────────────────────────
if (useDatabase)
{
    var connStr = cfg.GetConnectionString("Postgres")!;

    builder.Services.AddPgSqlScheduler(
        connectionString: connStr,
        schema: schema,
        configureOptions: ConfigureSchedulerOptions,
        assemblies: typeof(Program).Assembly);
}
else
{
    // In-memory Quartz store — no migrations, no persistence across restarts.
    builder.Services.AddScheduler(
        configureOptions: ConfigureSchedulerOptions,
        assemblies: typeof(Program).Assembly);
}

// ─────────────────────────────────────────────────────────────────────────────
// Job execution monitoring
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddSchedulerMonitoring<AppDbContext>();

// ─────────────────────────────────────────────────────────────────────────────
// Scheduler Admin UI
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddSchedulerViewer(opts =>
{
    opts.Title      = "Sample App Scheduler";
    opts.PathPrefix = "/scheduler-management";
    // opts.AuthorizeAsync = ctx => Task.FromResult(ctx.User.IsInRole("Admin"));
});

// ─────────────────────────────────────────────────────────────────────────────
// Build & configure pipeline
// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Seed sample customers so the jobs have data to work with from the start.
await SeedAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SW.Scheduler Sample v1"));
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.UseSchedulerViewer();
app.MapSchedulerViewer();

app.MapControllers();

app.Run();

// ─────────────────────────────────────────────────────────────────────────────
// Seed helper
// ─────────────────────────────────────────────────────────────────────────────
static async Task SeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Create the database and schema if they don't exist.
    // EnsureCreatedAsync works for both InMemory and real providers (PostgreSQL etc.).
    // It creates tables directly from the model without migrations — suitable for a sample app.
    await db.Database.EnsureCreatedAsync();

    if (await db.Customers.AnyAsync()) return;

    db.Customers.AddRange(
        new Customer { Name = "Alice Smith",   Email = "alice@acme.com"   },
        new Customer { Name = "Bob Jones",     Email = "bob@acme.com"     },
        new Customer { Name = "Carol White",   Email = "carol@globex.com" },
        new Customer { Name = "Dan Brown",     Email = "dan@globex.com"   },
        new Customer { Name = "Eva Green",     Email = "eva@initech.com"  }
    );
    await db.SaveChangesAsync();
}


