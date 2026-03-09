using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SW.Scheduler;

namespace SW.Scheduler.EfCore.EntityTypeConfigurations;

public class QuartzSchedulerStateEntityTypeConfiguration(string? prefix, string? schema, QuartzColumnTypes types)
    : IEntityTypeConfiguration<QuartzSchedulerState>
{
    public void Configure(EntityTypeBuilder<QuartzSchedulerState> builder)
    {
        builder.ToTable($"{prefix}scheduler_state", schema);
        builder.HasKey(x => new { x.SchedulerName, x.InstanceName });

        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.InstanceName).HasColumnName("instance_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.LastCheckInTime).HasColumnName("last_checkin_time").HasColumnType(types.BigInt).IsRequired();
        builder.Property(x => x.CheckInInterval).HasColumnName("checkin_interval").HasColumnType(types.BigInt).IsRequired();
    }
}

