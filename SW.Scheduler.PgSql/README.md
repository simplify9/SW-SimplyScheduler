# SW.Scheduler.PgSql

PostgreSQL persistence support for SW.Scheduler with automatic migration integration into your existing DbContext.

## 🎯 Key Features

- ✅ **Integrates with YOUR DbContext** - Quartz tables included in your migrations
- ✅ **Schema is REQUIRED** - PostgreSQL best practice
- ✅ **No separate migrations** - Everything in one place
- ✅ **Zero entity exposure** - Clean API, no direct entity access
- ✅ **Clustering support** - Multiple scheduler instances
- ✅ **Configurable** - Table prefix, clustering options

## 📦 Installation

```bash
dotnet add package SW.Scheduler.PgSql
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

## 🚀 Quick Start

### 1. Add Quartz Tables to Your DbContext

```csharp
using Microsoft.EntityFrameworkCore;
using SW.Scheduler.PgSql;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    // Your entities
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure your entities
        modelBuilder.Entity<Customer>(b =>
        {
            b.HasKey(c => c.Id);
            // ... other configuration
        });

        // ⭐ Add Quartz tables to YOUR migrations
        modelBuilder.UseQuartzPostgreSql(schema: "quartz");
    }
}
```

### 2. Create Migration

```bash
dotnet ef migrations add AddQuartzTables
dotnet ef database update
```

Your migration will now include both your entities AND Quartz tables! 🎉

### 3. Configure Scheduler in Program.cs

```csharp
using SW.Scheduler.PgSql;

var builder = WebApplication.CreateBuilder(args);

// Configure DbContext with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure Scheduler with PostgreSQL persistence
builder.Services.AddPgSqlScheduler(
    connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
    schema: "quartz"  // Same schema as in UseQuartzPostgreSql
);

var app = builder.Build();

// Apply migrations on startup (optional but recommended)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
```

That's it! ✨

## 📋 Configuration Options

### Basic Configuration

```csharp
builder.Services.AddPgSqlScheduler(
    connectionString: "Host=localhost;Database=myapp;Username=postgres;Password=secret",
    schema: "quartz"  // REQUIRED
);
```

### Advanced Configuration

```csharp
builder.Services.AddPgSqlScheduler(
    connectionString: connectionString,
    schema: "quartz",
    configure: options =>
    {
        options.TablePrefix = "qrtz_";  // Default prefix
        options.EnableClustering = true;  // Enable clustering
        options.ClusteringCheckinInterval = TimeSpan.FromSeconds(10);
        options.ClusteringMisfireThreshold = TimeSpan.FromSeconds(20);
    }
);
```

### Configuration Action Style

```csharp
builder.Services.AddPgSqlScheduler(options =>
{
    options.ConnectionString = configuration.GetConnectionString("DefaultConnection")!;
    options.Schema = "quartz";
    options.EnableClustering = true;
});
```

## 🏗️ Architecture

### Why This Approach?

**Traditional approach** (separate DbContext):
- ❌ Two separate migration histories
- ❌ Complex to manage
- ❌ Risk of out-of-sync databases

**Our approach** (integrated):
- ✅ One DbContext, one migration history
- ✅ Simple to manage
- ✅ Everything stays in sync
- ✅ No entity exposure

### How It Works

```
Your DbContext
    ↓
OnModelCreating
    ↓
modelBuilder.UseQuartzPostgreSql("quartz")
    ↓
Applies 11 Quartz entity configurations internally
    ↓
Your migration includes everything
```

## 📊 Database Schema

When you call `UseQuartzPostgreSql("quartz")`, these tables are created in the specified schema:

```
quartz.qrtz_job_details
quartz.qrtz_triggers
quartz.qrtz_simple_triggers
quartz.qrtz_cron_triggers
quartz.qrtz_simprop_triggers
quartz.qrtz_blob_triggers
quartz.qrtz_calendars
quartz.qrtz_paused_trigger_grps
quartz.qrtz_fired_triggers
quartz.qrtz_scheduler_state
quartz.qrtz_locks
```

**Schema is REQUIRED for PostgreSQL** - this is a best practice for multi-tenant and organized databases.

## 🔧 Common Scenarios

### Scenario 1: New Application

```csharp
// 1. Add to DbContext
modelBuilder.UseQuartzPostgreSql("quartz");

// 2. Create initial migration
dotnet ef migrations add Initial

// 3. Apply migration
dotnet ef database update
```

### Scenario 2: Existing Application

```csharp
// 1. Add to DbContext
modelBuilder.UseQuartzPostgreSql("quartz");

// 2. Create migration for Quartz tables
dotnet ef migrations add AddQuartzSupport

// 3. Apply migration
dotnet ef database update
```

### Scenario 3: Multiple Environments

```csharp
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=myapp_dev;..."
  },
  "Quartz": {
    "Schema": "quartz"
  }
}

// Program.cs
builder.Services.AddPgSqlScheduler(
    connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
    schema: builder.Configuration["Quartz:Schema"]!
);
```

### Scenario 4: Clustering (Multiple Instances)

```csharp
builder.Services.AddPgSqlScheduler(
    connectionString: connectionString,
    schema: "quartz",
    configure: options =>
    {
        options.EnableClustering = true;
        options.ClusteringCheckinInterval = TimeSpan.FromSeconds(10);
    }
);
```

## ⚠️ Important Notes

### Schema Requirement

PostgreSQL requires an explicit schema. This is enforced:

```csharp
// ✅ Good
modelBuilder.UseQuartzPostgreSql("quartz");

// ❌ Throws ArgumentException
modelBuilder.UseQuartzPostgreSql("");  // Schema cannot be empty
```

### Schema Consistency

The schema MUST match in both places:

```csharp
// In DbContext
modelBuilder.UseQuartzPostgreSql(schema: "quartz");

// In Program.cs
builder.Services.AddPgSqlScheduler(
    connectionString: connectionString,
    schema: "quartz"  // ⚠️ MUST match DbContext schema
);
```

### Table Prefix

Default is `qrtz_` but you can customize:

```csharp
// In DbContext
modelBuilder.UseQuartzPostgreSql(
    schema: "quartz",
    tablePrefix: "myapp_"  // Results in: quartz.myapp_job_details
);

// In Program.cs
builder.Services.AddPgSqlScheduler(
    connectionString: connectionString,
    schema: "quartz",
    configure: options =>
    {
        options.TablePrefix = "myapp_";  // ⚠️ MUST match DbContext prefix
    }
);
```

## 🧪 Testing

For integration tests, you can use an in-memory or test database:

```csharp
// TestDbContext
public class TestDbContext : AppDbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("Host=localhost;Database=test_db;...");
    }
}

// Test setup
var options = new DbContextOptionsBuilder<TestDbContext>()
    .UseNpgsql(testConnectionString)
    .Options;

using var context = new TestDbContext(options);
await context.Database.MigrateAsync();
```

## 📚 Examples

### Complete Example

See the [SampleApplication](../SampleApplication) for a working example with:
- DbContext with Quartz tables
- PostgreSQL configuration
- Migration setup
- Job definitions

### Migration File Example

When you run `dotnet ef migrations add AddQuartzSupport`, you'll get:

```csharp
public partial class AddQuartzSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "quartz");

        migrationBuilder.CreateTable(
            name: "qrtz_job_details",
            schema: "quartz",
            columns: table => new
            {
                sched_name = table.Column<string>(type: "text", nullable: false),
                job_name = table.Column<string>(type: "text", nullable: false),
                job_group = table.Column<string>(type: "text", nullable: false),
                // ... all Quartz columns
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_qrtz_job_details", x => new { x.sched_name, x.job_name, x.job_group });
            });
        
        // ... 10 more Quartz tables
    }
}
```

## 🆘 Troubleshooting

### Error: "Schema is required for PostgreSQL"

**Cause**: Called `UseQuartzPostgreSql()` without schema parameter.

**Solution**:
```csharp
modelBuilder.UseQuartzPostgreSql("quartz");  // Schema is required
```

### Error: "Table 'qrtz_job_details' does not exist"

**Cause**: Forgot to run migrations.

**Solution**:
```bash
dotnet ef database update
```

### Error: "Schema 'quartz' does not exist"

**Cause**: PostgreSQL schema not created.

**Solution**: EF Core automatically creates the schema when you run migrations.

### Warning: "Schema mismatch"

**Cause**: Schema in `UseQuartzPostgreSql()` doesn't match `AddPgSqlScheduler()`.

**Solution**: Ensure both use the same schema name.

## 🎯 Best Practices

1. **Use a dedicated schema** for Quartz tables (e.g., "quartz", "scheduler")
2. **Keep the same schema** in DbContext and Scheduler configuration
3. **Apply migrations on startup** in production (or use deployment scripts)
4. **Enable clustering** for high-availability scenarios
5. **Use connection pooling** in your connection string
6. **Monitor scheduler state** using the `quartz.qrtz_scheduler_state` table

## 🔗 Related Packages

- **SW.Scheduler** - Core scheduler library
- **SW.Scheduler.Sdk** - Interfaces for job definitions
- **SW.Scheduler.EfCore** - Base EF Core support
- **SW.Scheduler.MySql** - MySQL persistence (coming soon)
- **SW.Scheduler.SqlServer** - SQL Server persistence (coming soon)

## 📄 License

Same as SW.Scheduler parent project.

---

**Questions?** Check the [main documentation](../README.md) or open an issue.
