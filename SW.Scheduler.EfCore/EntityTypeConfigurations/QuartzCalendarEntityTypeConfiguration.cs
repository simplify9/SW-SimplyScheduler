using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SW.Scheduler;

namespace SW.Scheduler.EfCore.EntityTypeConfigurations;

public class QuartzCalendarEntityTypeConfiguration(string? prefix, string? schema, QuartzColumnTypes types)
    : IEntityTypeConfiguration<QuartzCalendar>
{
    public void Configure(EntityTypeBuilder<QuartzCalendar> builder)
    {
        builder.ToTable($"{prefix}calendars", schema);
        builder.HasKey(x => new { x.SchedulerName, x.CalendarName });

        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.CalendarName).HasColumnName("calendar_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.Calendar).HasColumnName("calendar").HasColumnType(types.Blob).IsRequired();
    }
}

