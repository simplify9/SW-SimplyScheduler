using Microsoft.EntityFrameworkCore;
using SW.Scheduler.PgSql.EntityTypeConfigurations;

namespace SW.Scheduler.PgSql;

public static class ModelBuilderExtensions
{
    public static void ApplySchedulerPgSqlDbModels(this ModelBuilder builder, string schema,
        string prefix = "qrtz_")
    {
        builder.ApplyConfiguration(
            new QuartzJobDetailEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzTriggerEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzSimpleTriggerEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzSimplePropertyTriggerEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzCronTriggerEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzBlobTriggerEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzCalendarEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzPausedTriggerGroupEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzFiredTriggerEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzSchedulerStateEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzLockEntityTypeConfiguration(prefix, schema));
    }
}