using System.Reflection;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SW.Scheduler;

namespace SW.Scheduler.EfCore;

public static class SchedulingModelBuilderExtensions
{
    public static ModelBuilder ApplyScheduling(this ModelBuilder modelBuilder, string? schema = null)
    {
        if (!string.IsNullOrWhiteSpace(schema))
        {
            try { modelBuilder.HasDefaultSchema(schema); } catch { /* ignore unsupported schema providers */ }
        }

        ApplyAllConfigurations(modelBuilder, typeof(SchedulingModelBuilderExtensions).Assembly);

        var providerName = TryGetProviderName(modelBuilder);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var name = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(name)) continue;
            string newName = name;
            if (providerName == "Npgsql.EntityFrameworkCore.PostgreSQL") newName = ToSnakeCase(name);
            else if (providerName == "Microsoft.EntityFrameworkCore.SqlServer") newName = ToPascalCase(name);
            if (newName != name) entityType.SetTableName(newName);
        }
        return modelBuilder;
    }

    private static void ApplyAllConfigurations(ModelBuilder modelBuilder, Assembly assembly)
    {
        var applyConfigMethod = typeof(ModelBuilder).GetMethods()
            .First(m => m.Name == nameof(ModelBuilder.ApplyConfiguration) && m.GetParameters().Length == 1);

        var configTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
            .Select(t => new { Type = t, Iface = t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)) })
            .Where(x => x.Iface != null)
            .ToList();

        foreach (var ct in configTypes)
        {
            var entityArg = ct.Iface!.GetGenericArguments()[0];
            var instance = Activator.CreateInstance(ct.Type);
            if (instance == null) continue;
            applyConfigMethod.MakeGenericMethod(entityArg).Invoke(modelBuilder, new[] { instance });
        }
    }

    private static string? TryGetProviderName(ModelBuilder modelBuilder)
    {
        var ann = modelBuilder.Model.GetAnnotations().FirstOrDefault(a => a.Name.EndsWith("ProviderName", StringComparison.OrdinalIgnoreCase));
        if (ann?.Value is string s && !string.IsNullOrWhiteSpace(s)) return s;
        ann = modelBuilder.Model.FindAnnotation("Relational:ProviderName");
        if (ann?.Value is string s2 && !string.IsNullOrWhiteSpace(s2)) return s2;
        return null;
    }

    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1])))) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        var parts = name.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(p[0]));
            if (p.Length > 1) sb.Append(p.Substring(1).ToLowerInvariant());
        }
        return sb.ToString();
    }
}

// ---------------- Entity Configurations ----------------

internal class QuartzBlobTriggerConfig : IEntityTypeConfiguration<QuartzBlobTrigger>
{
    public void Configure(EntityTypeBuilder<QuartzBlobTrigger> builder)
    {
        builder.ToTable("blob_triggers");
        builder.HasKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup });
        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").IsRequired();
        builder.Property(x => x.TriggerName).HasColumnName("trigger_name").IsRequired();
        builder.Property(x => x.TriggerGroup).HasColumnName("trigger_group").IsRequired();
        builder.Property(x => x.BlobData).HasColumnName("blob_data");
        builder.HasOne(x => x.Trigger).WithMany(x => x.BlobTriggers)
            .HasForeignKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal class QuartzCalendarConfig : IEntityTypeConfiguration<QuartzCalendar>
{
    public void Configure(EntityTypeBuilder<QuartzCalendar> builder)
    {
        builder.ToTable("calendars");
        builder.HasKey(x => new { x.SchedulerName, x.CalendarName });
        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").IsRequired();
        builder.Property(x => x.CalendarName).HasColumnName("calendar_name").IsRequired();
        builder.Property(x => x.Calendar).HasColumnName("calendar").IsRequired();
    }
}

internal class QuartzCronTriggerConfig : IEntityTypeConfiguration<QuartzCronTrigger>
{
    public void Configure(EntityTypeBuilder<QuartzCronTrigger> builder)
    {
        builder.ToTable("cron_triggers");
        builder.HasKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup });
        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").IsRequired();
        builder.Property(x => x.TriggerName).HasColumnName("trigger_name").IsRequired();
        builder.Property(x => x.TriggerGroup).HasColumnName("trigger_group").IsRequired();
        builder.Property(x => x.CronExpression).HasColumnName("cron_expression").IsRequired();
        builder.Property(x => x.TimeZoneId).HasColumnName("time_zone_id");
        builder.HasOne(x => x.Trigger).WithMany(x => x.CronTriggers)
            .HasForeignKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal class QuartzFiredTriggerConfig : IEntityTypeConfiguration<QuartzFiredTrigger>
{
    public void Configure(EntityTypeBuilder<QuartzFiredTrigger> builder)
    {
        builder.ToTable("fired_triggers");
        builder.HasKey(x => new { x.SchedulerName, x.EntryId });
        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").IsRequired();
        builder.Property(x => x.EntryId).HasColumnName("entry_id").IsRequired();
        builder.Property(x => x.TriggerName).HasColumnName("trigger_name").IsRequired();
        builder.Property(x => x.TriggerGroup).HasColumnName("trigger_group").IsRequired();
        builder.Property(x => x.InstanceName).HasColumnName("instance_name").IsRequired();
        builder.Property(x => x.FiredTime).HasColumnName("fired_time").IsRequired();
        builder.Property(x => x.ScheduledTime).HasColumnName("sched_time").IsRequired();
        builder.Property(x => x.Priority).HasColumnName("priority").IsRequired();
        builder.Property(x => x.State).HasColumnName("state").IsRequired();
        builder.Property(x => x.JobName).HasColumnName("job_name");
        builder.Property(x => x.JobGroup).HasColumnName("job_group");
        builder.Property(x => x.IsNonConcurrent).HasColumnName("is_nonconcurrent").IsRequired();
        builder.Property(x => x.RequestsRecovery).HasColumnName("requests_recovery");
        builder.HasIndex(x => x.TriggerName).HasDatabaseName("idx_ft_trig_name");
        builder.HasIndex(x => x.TriggerGroup).HasDatabaseName("idx_ft_trig_group");
        builder.HasIndex(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup }).HasDatabaseName("idx_ft_trig_nm_gp");
        builder.HasIndex(x => x.InstanceName).HasDatabaseName("idx_ft_trig_inst_name");
        builder.HasIndex(x => x.JobName).HasDatabaseName("idx_ft_job_name");
        builder.HasIndex(x => x.JobGroup).HasDatabaseName("idx_ft_job_group");
        builder.HasIndex(x => x.RequestsRecovery).HasDatabaseName("idx_ft_job_req_recovery");
    }
}

internal class QuartzJobDetailConfig : IEntityTypeConfiguration<QuartzJobDetail>
{
    public void Configure(EntityTypeBuilder<QuartzJobDetail> builder)
    {
        builder.ToTable("job_details");
        builder.HasKey(x => new { x.SchedulerName, x.JobName, x.JobGroup });
        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").IsRequired();
        builder.Property(x => x.JobName).HasColumnName("job_name").IsRequired();
        builder.Property(x => x.JobGroup).HasColumnName("job_group").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.JobClassName).HasColumnName("job_class_name").IsRequired();
        builder.Property(x => x.IsDurable).HasColumnName("is_durable").IsRequired();
        builder.Property(x => x.IsNonConcurrent).HasColumnName("is_nonconcurrent").IsRequired();
        builder.Property(x => x.IsUpdateData).HasColumnName("is_update_data").IsRequired();
        builder.Property(x => x.RequestsRecovery).HasColumnName("requests_recovery").IsRequired();
        builder.Property(x => x.JobData).HasColumnName("job_data");
        builder.HasIndex(x => x.RequestsRecovery).HasDatabaseName("idx_j_req_recovery");
    }
}

internal class QuartzLockConfig : IEntityTypeConfiguration<QuartzLock>
{
    public void Configure(EntityTypeBuilder<QuartzLock> builder)
    {
        builder.ToTable("locks");
        builder.HasKey(x => new { x.SchedulerName, x.LockName });
        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").IsRequired();
        builder.Property(x => x.LockName).HasColumnName("lock_name").IsRequired();
    }
}

internal class QuartzPausedTriggerGroupConfig : IEntityTypeConfiguration<QuartzPausedTriggerGroup>
{
    public void Configure(EntityTypeBuilder<QuartzPausedTriggerGroup> builder)
    {
        builder.ToTable("paused_trigger_grps");
        builder.HasKey(x => new { x.SchedulerName, x.TriggerGroup });
        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").IsRequired();
        builder.Property(x => x.TriggerGroup).HasColumnName("trigger_group").IsRequired();
    }
}

internal class QuartzSchedulerStateConfig : IEntityTypeConfiguration<QuartzSchedulerState>
{
    public void Configure(EntityTypeBuilder<QuartzSchedulerState> builder)
    {
        builder.ToTable("scheduler_state");
        builder.HasKey(x => new { x.SchedulerName, x.InstanceName });
        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").IsRequired();
        builder.Property(x => x.InstanceName).HasColumnName("instance_name").IsRequired();
        builder.Property(x => x.LastCheckInTime).HasColumnName("last_checkin_time").IsRequired();
        builder.Property(x => x.CheckInInterval).HasColumnName("checkin_interval").IsRequired();
    }
}

internal class QuartzSimpleTriggerConfig : IEntityTypeConfiguration<QuartzSimpleTrigger>
{
    public void Configure(EntityTypeBuilder<QuartzSimpleTrigger> builder)
    {
        builder.ToTable("simple_triggers");
        builder.HasKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup });
        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").IsRequired();
        builder.Property(x => x.TriggerName).HasColumnName("trigger_name").IsRequired();
        builder.Property(x => x.TriggerGroup).HasColumnName("trigger_group").IsRequired();
        builder.Property(x => x.RepeatCount).HasColumnName("repeat_count").IsRequired();
        builder.Property(x => x.RepeatInterval).HasColumnName("repeat_interval").IsRequired();
        builder.Property(x => x.TimesTriggered).HasColumnName("times_triggered").IsRequired();
        builder.HasOne(x => x.Trigger).WithMany(x => x.SimpleTriggers)
            .HasForeignKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal class QuartzSimplePropertyTriggerConfig : IEntityTypeConfiguration<QuartzSimplePropertyTrigger>
{
    public void Configure(EntityTypeBuilder<QuartzSimplePropertyTrigger> builder)
    {
        builder.ToTable("simprop_triggers");
        builder.HasKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup });
        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").IsRequired();
        builder.Property(x => x.TriggerName).HasColumnName("trigger_name").IsRequired();
        builder.Property(x => x.TriggerGroup).HasColumnName("trigger_group").IsRequired();
        builder.Property(x => x.StringProperty1).HasColumnName("str_prop_1");
        builder.Property(x => x.StringProperty2).HasColumnName("str_prop_2");
        builder.Property(x => x.StringProperty3).HasColumnName("str_prop_3");
        builder.Property(x => x.IntegerProperty1).HasColumnName("int_prop_1");
        builder.Property(x => x.IntegerProperty2).HasColumnName("int_prop_2");
        builder.Property(x => x.LongProperty1).HasColumnName("long_prop_1");
        builder.Property(x => x.LongProperty2).HasColumnName("long_prop_2");
        builder.Property(x => x.DecimalProperty1).HasColumnName("dec_prop_1");
        builder.Property(x => x.DecimalProperty2).HasColumnName("dec_prop_2");
        builder.Property(x => x.BooleanProperty1).HasColumnName("bool_prop_1");
        builder.Property(x => x.BooleanProperty2).HasColumnName("bool_prop_2");
        builder.Property(x => x.TimeZoneId).HasColumnName("time_zone_id");
        builder.HasOne(x => x.Trigger).WithMany(x => x.SimplePropertyTriggers)
            .HasForeignKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal class QuartzTriggerConfig : IEntityTypeConfiguration<QuartzTrigger>
{
    public void Configure(EntityTypeBuilder<QuartzTrigger> builder)
    {
        builder.ToTable("triggers");
        builder.HasKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup });
        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").IsRequired();
        builder.Property(x => x.TriggerName).HasColumnName("trigger_name").IsRequired();
        builder.Property(x => x.TriggerGroup).HasColumnName("trigger_group").IsRequired();
        builder.Property(x => x.JobName).HasColumnName("job_name").IsRequired();
        builder.Property(x => x.JobGroup).HasColumnName("job_group").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.NextFireTime).HasColumnName("next_fire_time");
        builder.Property(x => x.PreviousFireTime).HasColumnName("prev_fire_time");
        builder.Property(x => x.Priority).HasColumnName("priority");
        builder.Property(x => x.TriggerState).HasColumnName("trigger_state").IsRequired();
        builder.Property(x => x.TriggerType).HasColumnName("trigger_type").IsRequired();
        builder.Property(x => x.StartTime).HasColumnName("start_time").IsRequired();
        builder.Property(x => x.EndTime).HasColumnName("end_time");
        builder.Property(x => x.CalendarName).HasColumnName("calendar_name");
        builder.Property(x => x.MisfireInstruction).HasColumnName("misfire_instr");
        builder.Property(x => x.JobData).HasColumnName("job_data");
        builder.HasOne(x => x.JobDetail).WithMany(x => x.Triggers)
            .HasForeignKey(x => new { x.SchedulerName, x.JobName, x.JobGroup }).IsRequired();
        builder.HasIndex(x => x.NextFireTime).HasDatabaseName("idx_t_next_fire_time");
        builder.HasIndex(x => x.TriggerState).HasDatabaseName("idx_t_state");
        builder.HasIndex(x => new { x.NextFireTime, x.TriggerState }).HasDatabaseName("idx_t_nft_st");
    }
}
