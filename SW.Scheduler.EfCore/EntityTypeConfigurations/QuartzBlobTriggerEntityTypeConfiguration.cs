using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SW.Scheduler;

namespace SW.Scheduler.EfCore.EntityTypeConfigurations;

public class QuartzBlobTriggerEntityTypeConfiguration(string? prefix, string? schema, QuartzColumnTypes types)
    : IEntityTypeConfiguration<QuartzBlobTrigger>
{
    public void Configure(EntityTypeBuilder<QuartzBlobTrigger> builder)
    {
        builder.ToTable($"{prefix}blob_triggers", schema);
        builder.HasKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup });

        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.TriggerName).HasColumnName("trigger_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.TriggerGroup).HasColumnName("trigger_group").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.BlobData).HasColumnName("blob_data").HasColumnType(types.Blob);

        builder.HasOne(x => x.Trigger).WithMany(x => x.BlobTriggers)
            .HasForeignKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

