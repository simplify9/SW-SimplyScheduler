using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SW.Scheduler;

namespace SW.Scheduler.EfCore.EntityTypeConfigurations;

public class QuartzSimpleTriggerEntityTypeConfiguration(string? prefix, string? schema, QuartzColumnTypes types)
    : IEntityTypeConfiguration<QuartzSimpleTrigger>
{
    public void Configure(EntityTypeBuilder<QuartzSimpleTrigger> builder)
    {
        builder.ToTable($"{prefix}simple_triggers", schema);
        builder.HasKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup });

        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.TriggerName).HasColumnName("trigger_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.TriggerGroup).HasColumnName("trigger_group").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.RepeatCount).HasColumnName("repeat_count").HasColumnType(types.BigInt).IsRequired();
        builder.Property(x => x.RepeatInterval).HasColumnName("repeat_interval").HasColumnType(types.BigInt).IsRequired();
        builder.Property(x => x.TimesTriggered).HasColumnName("times_triggered").HasColumnType(types.BigInt).IsRequired();

        builder.HasOne(x => x.Trigger).WithMany(x => x.SimpleTriggers)
            .HasForeignKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

