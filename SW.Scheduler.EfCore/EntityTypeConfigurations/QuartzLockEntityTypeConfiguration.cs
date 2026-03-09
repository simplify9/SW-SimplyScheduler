using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SW.Scheduler;

namespace SW.Scheduler.EfCore.EntityTypeConfigurations;

public class QuartzLockEntityTypeConfiguration(string? prefix, string? schema, QuartzColumnTypes types)
    : IEntityTypeConfiguration<QuartzLock>
{
    public void Configure(EntityTypeBuilder<QuartzLock> builder)
    {
        builder.ToTable($"{prefix}locks", schema);
        builder.HasKey(x => new { x.SchedulerName, x.LockName });

        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.LockName).HasColumnName("lock_name").HasColumnType(types.Text).IsRequired();
    }
}

