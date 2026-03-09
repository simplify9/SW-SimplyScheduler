using Microsoft.EntityFrameworkCore;
using SW.Scheduler;
using SW.Scheduler.EfCore;
using SampleApplication.Data;
using SampleApplication;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────────────────
// Infrastructure
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SW.Scheduler Sample", Version = "v1" });
});

// ─────────────────────────────────────────────────────────────────────────────
// EF Core — InMemory for the sample (swap for a real provider in production)
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseInMemoryDatabase("SampleAppDb"));

// ─────────────────────────────────────────────────────────────────────────────
// Scheduler
//
// OPTION A (used here): In-memory Quartz store — great for development/testing.
//   Jobs are lost on restart. No migrations needed.
//
// OPTION B: PostgreSQL persistent store — recommended for production.
//   Replace the block below with:
//
//     builder.Services.AddPgSqlScheduler(
//         connectionString: builder.Configuration.GetConnectionString("Postgres")!,
//         schema: "quartz",
//         configureOptions: options => { /* same options as below */ },
//         assemblies: typeof(Program).Assembly);
//
//   And in AppDbContext.OnModelCreating:
//     modelBuilder.UseQuartzPostgreSql("quartz");
//
// OPTION C: SQL Server
//     builder.Services.AddSqlServerScheduler(
//         connectionString: builder.Configuration.GetConnectionString("SqlServer")!,
//         assemblies: typeof(Program).Assembly);
//     modelBuilder.UseQuartzSqlServer();
//
// OPTION D: MySQL / MariaDB
//     builder.Services.AddMySqlScheduler(
//         connectionString: builder.Configuration.GetConnectionString("MySql")!,
//         assemblies: typeof(Program).Assembly);
//     modelBuilder.UseQuartzMySql();
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddScheduler(
    configureOptions: options =>
    {
        // Identity used as the RequestContext user during job execution.
        options.SystemUserIdentifier  = "sample-scheduler";

        // Execution history: keep records for 30 days, clean up daily at 2 AM.
        options.RetentionDays         = 30;
        options.CleanupCronExpression = "0 0 2 * * ?";

        // Cloud archiving: set EnableArchive = true and register ICloudFilesService
        // to upload execution JSON to S3/Azure/GCS after each job completes.
        options.EnableArchive         = false;
        options.CloudFilesPrefix      = "sample-app/";
    },
    assemblies: typeof(Program).Assembly   // scan this assembly for IScheduledJob implementations
);

// ─────────────────────────────────────────────────────────────────────────────
// Job execution monitoring
//
// Wires IJobExecutionStore → AppDbContext so every execution is recorded in
// the job_executions table. Requires AppDbContext.OnModelCreating to call
// modelBuilder.ApplyScheduling() (or the provider-specific equivalent).
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddSchedulerMonitoring<AppDbContext>();

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
app.MapControllers();

app.Run();

// ─────────────────────────────────────────────────────────────────────────────
// Seed helper
// ─────────────────────────────────────────────────────────────────────────────
static async Task SeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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
