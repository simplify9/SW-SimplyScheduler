using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SW.Scheduler;

namespace SW.Scheduler.EfCore.EntityTypeConfigurations;

public class QuartzJobDetailEntityTypeConfiguration(string? prefix, string? schema, QuartzColumnTypes types)
    : IEntityTypeConfiguration<QuartzJobDetail>
{
    public void Configure(EntityTypeBuilder<QuartzJobDetail> builder)
    {
        builder.ToTable($"{prefix}job_details", schema);
        builder.HasKey(x => new { x.SchedulerName, x.JobName, x.JobGroup });

        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.JobName).HasColumnName("job_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.JobGroup).HasColumnName("job_group").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType(types.Text);
        builder.Property(x => x.JobClassName).HasColumnName("job_class_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.IsDurable).HasColumnName("is_durable").HasColumnType(types.Bool).IsRequired();
        builder.Property(x => x.IsNonConcurrent).HasColumnName("is_nonconcurrent").HasColumnType(types.Bool).IsRequired();
        builder.Property(x => x.IsUpdateData).HasColumnName("is_update_data").HasColumnType(types.Bool).IsRequired();
        builder.Property(x => x.RequestsRecovery).HasColumnName("requests_recovery").HasColumnType(types.Bool).IsRequired();
        builder.Property(x => x.JobData).HasColumnName("job_data").HasColumnType(types.Blob);

        builder.HasIndex(x => x.RequestsRecovery).HasDatabaseName("idx_j_req_recovery");
    }
}

