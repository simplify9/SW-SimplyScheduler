using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SW.Scheduler;

namespace SW.Scheduler.EfCore.EntityTypeConfigurations;

/// <summary>
/// Provider-aware EF Core configuration for <see cref="JobExecution"/>.
/// The <paramref name="types"/> argument controls the actual SQL column types used,
/// allowing this single class to work across PostgreSQL, SQL Server, and MySQL.
/// </summary>
public class JobExecutionEntityTypeConfiguration(string? schema, QuartzColumnTypes types)
    : IEntityTypeConfiguration<JobExecution>
{
    public void Configure(EntityTypeBuilder<JobExecution> builder)
    {
        builder.ToTable("job_executions", schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(x => x.JobName).HasColumnName("job_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.JobGroup).HasColumnName("job_group").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.JobTypeName).HasColumnName("job_type_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.FireInstanceId).HasColumnName("fire_instance_id").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.StartTimeUtc).HasColumnName("start_time_utc").IsRequired();
        builder.Property(x => x.EndTimeUtc).HasColumnName("end_time_utc");
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms");
        builder.Property(x => x.Success).HasColumnName("success");
        builder.Property(x => x.Error).HasColumnName("error").HasColumnType(types.UnboundedText);
        builder.Property(x => x.Node).HasColumnName("node").HasColumnType(types.Text).IsRequired();

        // Context is the serialized ScheduledJobContext JSON — unbounded text, no max length.
        builder.Property(x => x.Context).HasColumnName("context").HasColumnType(types.UnboundedText);

        builder.HasIndex(x => new { x.JobGroup, x.JobName, x.StartTimeUtc }).HasDatabaseName("idx_je_group_name_start");
        builder.HasIndex(x => x.StartTimeUtc).HasDatabaseName("idx_je_start_time");
        builder.HasIndex(x => x.FireInstanceId).IsUnique().HasDatabaseName("idx_je_fire_instance_id");
        builder.HasIndex(x => x.Success).HasDatabaseName("idx_je_success");
    }
}

