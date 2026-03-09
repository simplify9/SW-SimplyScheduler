using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SW.Scheduler;

namespace SW.Scheduler.EfCore.EntityTypeConfigurations;

public class QuartzCronTriggerEntityTypeConfiguration(string? prefix, string? schema, QuartzColumnTypes types)
    : IEntityTypeConfiguration<QuartzCronTrigger>
{
    public void Configure(EntityTypeBuilder<QuartzCronTrigger> builder)
    {
        builder.ToTable($"{prefix}cron_triggers", schema);
        builder.HasKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup });

        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.TriggerName).HasColumnName("trigger_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.TriggerGroup).HasColumnName("trigger_group").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.CronExpression).HasColumnName("cron_expression").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasColumnType(types.Text);

        builder.HasOne(x => x.Trigger).WithMany(x => x.CronTriggers)
            .HasForeignKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

