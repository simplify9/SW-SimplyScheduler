# PostgreSQL Integration Guide

## ✅ Implementation Complete

SW.Scheduler.PgSql now provides **seamless integration** with your existing DbContext. Quartz tables become part of YOUR migrations - no separate DbContext needed!

---

## 🎯 Key Approach

**Users include Quartz tables in their own DbContext:**
- ✅ One DbContext
- ✅ One migration history  
- ✅ No entity exposure
- ✅ Clean API
- ✅ Schema is REQUIRED (PostgreSQL best practice)

---

## 📝 Implementation Summary

### Files Created

1. **QuartzModelBuilderPostgreSQLExtensions.cs** (Updated)
   - `UseQuartzPostgreSql(schema, tablePrefix)` - Main extension method
   - Applies all 11 Quartz entity configurations internally
   - No entity exposure to users

2. **ServiceCollectionExtensions.cs** (Updated)
   - `AddPgSqlScheduler(connectionString, schema, configure)` - Registers Quartz
   - Schema validation enforced
   - Clustering support
   - No DbContext registration (users manage their own)

3. **QuartzPgSqlOptions.cs**
   - Configuration POCO
   - Schema requirement enforced
   - Clustering options

4. **README.md**
   - Complete usage documentation
   - Examples for all scenarios
   - Troubleshooting guide

### Files Removed

- ❌ QuartzPgSqlDbContext.cs - Not needed, users use their own
- ❌ QuartzPgSqlDbContextFactory.cs - Not needed
- ❌ QuartzMigrationService.cs - Users handle migrations

---

## 🚀 How It Works

### Step 1: User adds to their DbContext

```csharp
public class AppDbContext : DbContext
{
    // User's entities
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User's configuration
        modelBuilder.Entity<Customer>()...
        
        // ⭐ Add Quartz tables (one line!)
        modelBuilder.UseQuartzPostgreSql("quartz");
    }
}
```

### Step 2: User creates migration

```bash
dotnet ef migrations add AddQuartzSupport
```

**Result**: Migration includes Customer tables AND Quartz tables! 🎉

### Step 3: User configures scheduler

```csharp
// Program.cs
builder.Services.AddPgSqlScheduler(
    connectionString: connectionString,
    schema: "quartz"  // Same as in DbContext
);
```

---

## 🏗️ Architecture Benefits

### vs. Separate DbContext Approach

| Aspect | Separate DbContext | Integrated (Our Approach) |
|--------|-------------------|---------------------------|
| **Migration History** | Two separate | One unified ✅ |
| **Complexity** | High | Low ✅ |
| **Sync Issues** | Possible | None ✅ |
| **User Effort** | More | Minimal ✅ |
| **Entity Exposure** | Yes | No ✅ |

### What Users DON'T See

Users never interact with Quartz entities directly:
- They don't add `DbSet<QuartzJobDetail>`
- They don't see entity configurations
- They just call `UseQuartzPostgreSql()`
- Magic happens internally ✨

---

## 📋 Entity Configurations

Internally, `UseQuartzPostgreSql()` applies these configurations:

1. QuartzJobDetailEntityTypeConfiguration
2. QuartzTriggerEntityTypeConfiguration
3. QuartzSimpleTriggerEntityTypeConfiguration
4. QuartzSimplePropertyTriggerEntityTypeConfiguration
5. QuartzCronTriggerEntityTypeConfiguration
6. QuartzBlobTriggerEntityTypeConfiguration
7. QuartzCalendarEntityTypeConfiguration
8. QuartzPausedTriggerGroupEntityTypeConfiguration
9. QuartzFiredTriggerEntityTypeConfiguration
10. QuartzSchedulerStateEntityTypeConfiguration
11. QuartzLockEntityTypeConfiguration

All configured for PostgreSQL with proper:
- Schema support
- Table naming (prefix)
- Primary keys
- Foreign keys
- Indexes
- Column types

---

## 🎯 Schema Requirement

PostgreSQL best practice is to use schemas. We enforce this:

```csharp
// ✅ Valid
modelBuilder.UseQuartzPostgreSql("quartz");
modelBuilder.UseQuartzPostgreSql("scheduler");
modelBuilder.UseQuartzPostgreSql("my_app_jobs");

// ❌ Invalid - throws ArgumentException
modelBuilder.UseQuartzPostgreSql("");
modelBuilder.UseQuartzPostgreSql(null);
```

### Why Schema is Required?

1. **Organization** - Separates Quartz tables from business tables
2. **Multi-tenancy** - Different schemas for different tenants
3. **Permissions** - Schema-level access control
4. **Best Practice** - Standard PostgreSQL pattern
5. **Clarity** - Clear what belongs to scheduler

---

## 🔧 Configuration Options

### Basic (Most Common)

```csharp
builder.Services.AddPgSqlScheduler(
    connectionString: "Host=localhost;Database=myapp;...",
    schema: "quartz"
);
```

### With Clustering

```csharp
builder.Services.AddPgSqlScheduler(
    connectionString: connectionString,
    schema: "quartz",
    configure: options =>
    {
        options.EnableClustering = true;
        options.ClusteringCheckinInterval = TimeSpan.FromSeconds(10);
        options.ClusteringMisfireThreshold = TimeSpan.FromSeconds(20);
    }
);
```

### Custom Table Prefix

```csharp
// In DbContext
modelBuilder.UseQuartzPostgreSql("quartz", "myapp_");

// In Program.cs
builder.Services.AddPgSqlScheduler(
    connectionString: connectionString,
    schema: "quartz",
    configure: options =>
    {
        options.TablePrefix = "myapp_";  // Must match!
    }
);
```

---

## 📊 Example Migration

When user runs `dotnet ef migrations add AddQuartz`, they get:

```csharp
public partial class AddQuartz : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create schema
        migrationBuilder.EnsureSchema(name: "quartz");

        // Create job_details table
        migrationBuilder.CreateTable(
            name: "qrtz_job_details",
            schema: "quartz",
            columns: table => new
            {
                sched_name = table.Column<string>(type: "text", nullable: false),
                job_name = table.Column<string>(type: "text", nullable: false),
                job_group = table.Column<string>(type: "text", nullable: false),
                description = table.Column<string>(type: "text", nullable: true),
                job_class_name = table.Column<string>(type: "text", nullable: false),
                is_durable = table.Column<bool>(type: "bool", nullable: false),
                is_nonconcurrent = table.Column<bool>(type: "bool", nullable: false),
                is_update_data = table.Column<bool>(type: "bool", nullable: false),
                requests_recovery = table.Column<bool>(type: "bool", nullable: false),
                job_data = table.Column<byte[]>(type: "bytea", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_qrtz_job_details", 
                    x => new { x.sched_name, x.job_name, x.job_group });
            });

        // ... 10 more tables with proper relationships
    }
}
```

---

## ✅ Validation

### Schema Validation

```csharp
// In QuartzPgSqlOptions.Validate()
if (string.IsNullOrWhiteSpace(Schema))
    throw new ArgumentException("Schema is required for PostgreSQL");
```

### Consistency Validation

Both must use same schema:
- `UseQuartzPostgreSql(schema)` in DbContext
- `AddPgSqlScheduler(..., schema)` in Program.cs

Quartz will fail at runtime if schemas don't match.

---

## 🧪 Testing Approach

Users can test with:

```csharp
[Fact]
public async Task Quartz_Tables_Are_Created()
{
    // Arrange
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(testConnectionString)
        .Options;
    
    using var context = new AppDbContext(options);
    
    // Act
    await context.Database.MigrateAsync();
    
    // Assert
    var tables = await context.Database
        .SqlQueryRaw<string>("SELECT table_name FROM information_schema.tables WHERE table_schema = 'quartz'")
        .ToListAsync();
    
    Assert.Contains("qrtz_job_details", tables);
    Assert.Contains("qrtz_triggers", tables);
    // ... assert all 11 tables exist
}
```

---

## 📦 Package Dependencies

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="8.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
<PackageReference Include="Quartz" Version="3.15.0" />
<PackageReference Include="Quartz.Extensions.Hosting" Version="3.15.0" />
<PackageReference Include="Quartz.Serialization.SystemTextJson" Version="3.15.0" />
```

---

## 🎓 Design Decisions

### Why No Separate DbContext?

**Considered**: Creating `QuartzPgSqlDbContext` for users
**Rejected**: Because:
1. Users have to manage two DbContexts
2. Two migration histories to maintain
3. Risk of schema drift
4. More complex deployment
5. Harder to test

**Chosen**: Extension method on user's DbContext
**Benefits**:
1. One DbContext, one source of truth
2. One migration history
3. Everything in sync
4. Simple deployment
5. Easy testing
6. No entity exposure

### Why Schema is Required?

**Considered**: Making schema optional with default "public"
**Rejected**: Because:
1. Not PostgreSQL best practice
2. Mixes business and infrastructure tables
3. Harder to manage permissions
4. Less organized

**Chosen**: Enforce schema requirement
**Benefits**:
1. Forces good PostgreSQL patterns
2. Clear separation of concerns
3. Better security model
4. Professional approach

### Why No Auto-Migration?

**Considered**: Auto-apply migrations on startup
**Rejected**: Because:
1. Users lose control over migrations
2. Risky in production
3. Not standard EF Core pattern
4. Hard to rollback

**Chosen**: Users manage their own migrations
**Benefits**:
1. Users have full control
2. Standard EF Core workflow
3. Safe for production
4. Easy to review changes
5. Can use migration scripts

---

## 🚀 Next Steps

### For MySQL Support

Create `SW.Scheduler.MySql` with:
- `UseQuartzMySql(schema?)` - schema optional for MySQL
- Same pattern as PostgreSQL
- MySQL-specific entity configurations

### For SQL Server Support

Create `SW.Scheduler.SqlServer` with:
- `UseQuartzSqlServer(schema?)` - schema optional (uses dbo by default)
- Same pattern
- SQL Server-specific configurations

---

## 📝 Summary

✅ **Clean API** - One method call: `UseQuartzPostgreSql()`  
✅ **No entity exposure** - Users never see Quartz entities  
✅ **Integrated migrations** - Everything in one place  
✅ **Schema enforced** - PostgreSQL best practice  
✅ **Well documented** - Complete README with examples  
✅ **Tested pattern** - Works in SampleApplication  
✅ **Production ready** - Safe and reliable  

**The user just calls one method in their DbContext, and Quartz tables are automatically included in their migrations!** 🎉

---

Made with ❤️ for clean architecture and developer happiness!
