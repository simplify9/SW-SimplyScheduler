using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SW.Scheduler;

namespace SW.Scheduler.EfCore.EntityTypeConfigurations;

public class QuartzSimplePropertyTriggerEntityTypeConfiguration(string? prefix, string? schema, QuartzColumnTypes types)
    : IEntityTypeConfiguration<QuartzSimplePropertyTrigger>
{
    public void Configure(EntityTypeBuilder<QuartzSimplePropertyTrigger> builder)
    {
        builder.ToTable($"{prefix}simprop_triggers", schema);
        builder.HasKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup });

        builder.Property(x => x.SchedulerName).HasColumnName("sched_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.TriggerName).HasColumnName("trigger_name").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.TriggerGroup).HasColumnName("trigger_group").HasColumnType(types.Text).IsRequired();
        builder.Property(x => x.StringProperty1).HasColumnName("str_prop_1").HasColumnType(types.Text);
        builder.Property(x => x.StringProperty2).HasColumnName("str_prop_2").HasColumnType(types.Text);
        builder.Property(x => x.StringProperty3).HasColumnName("str_prop_3").HasColumnType(types.Text);
        builder.Property(x => x.IntegerProperty1).HasColumnName("int_prop_1").HasColumnType(types.Int);
        builder.Property(x => x.IntegerProperty2).HasColumnName("int_prop_2").HasColumnType(types.Int);
        builder.Property(x => x.LongProperty1).HasColumnName("long_prop_1").HasColumnType(types.BigInt);
        builder.Property(x => x.LongProperty2).HasColumnName("long_prop_2").HasColumnType(types.BigInt);
        // Decimal properties use numeric/decimal — not in the QuartzColumnTypes helper since it's
        // universally "numeric" across all providers.
        builder.Property(x => x.DecimalProperty1).HasColumnName("dec_prop_1").HasColumnType("numeric");
        builder.Property(x => x.DecimalProperty2).HasColumnName("dec_prop_2").HasColumnType("numeric");
        builder.Property(x => x.BooleanProperty1).HasColumnName("bool_prop_1").HasColumnType(types.Bool);
        builder.Property(x => x.BooleanProperty2).HasColumnName("bool_prop_2").HasColumnType(types.Bool);
        builder.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasColumnType(types.Text);

        builder.HasOne(x => x.Trigger).WithMany(x => x.SimplePropertyTriggers)
            .HasForeignKey(x => new { x.SchedulerName, x.TriggerName, x.TriggerGroup })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

