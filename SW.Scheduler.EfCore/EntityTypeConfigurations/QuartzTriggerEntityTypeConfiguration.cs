using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SW.Scheduler;

namespace SW.Scheduler.EfCore.EntityTypeConfigurations;

public class QuartzTriggerEntityTypeConfiguration(string? prefix, string? schema, QuartzColumnTypes types)
    : IEntityTypeConfiguration<QuartzTrigger>
{
    public void Configure(EntityTypeBuilder<QuartzTrigger> builder)
    {
        builder.ToTable($"{prefix}triggers", schema);
        builder.HasKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup });

        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.TriggerName).HasColumnName("trigger_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.TriggerGroup).HasColumnName("trigger_group").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.JobName).HasColumnName("job_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.JobGroup).HasColumnName("job_group").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType(types.Text);
        builder.Property(x => x.NextFireTime).HasColumnName("next_fire_time").HasColumnType(types.BigInt);
        builder.Property(x => x.PreviousFireTime).HasColumnName("prev_fire_time").HasColumnType(types.BigInt);
        builder.Property(x => x.Priority).HasColumnName("priority").HasColumnType(types.Int);
        builder.Property(x => x.TriggerState).HasColumnName("trigger_state").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.TriggerType).HasColumnName("trigger_type").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.StartTime).HasColumnName("start_time").HasColumnType(types.BigInt).IsRequired();
        builder.Property(x => x.EndTime).HasColumnName("end_time").HasColumnType(types.BigInt);
        builder.Property(x => x.CalendarName).HasColumnName("calendar_name").HasColumnType(types.Text);
        builder.Property(x => x.MisfireInstruction).HasColumnName("misfire_instr").HasColumnType(types.Int);
        builder.Property(x => x.JobData).HasColumnName("job_data").HasColumnType(types.Blob);

        builder.HasOne(x => x.JobDetail).WithMany(x => x.Triggers)
            .HasForeignKey(x => new { x.SchedulerName, x.JobName, x.JobGroup }).IsRequired();

        builder.HasIndex(x => x.NextFireTime).HasDatabaseName("idx_t_next_fire_time");
        builder.HasIndex(x => x.TriggerState).HasDatabaseName("idx_t_state");
        builder.HasIndex(x => new { x.NextFireTime, x.TriggerState }).HasDatabaseName("idx_t_nft_st");
    }
}

