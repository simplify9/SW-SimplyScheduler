using Quartz;
using SW.PrimitiveTypes;

namespace SW.Scheduler;


public abstract class ScheduleBase:ISchedule
{
    internal abstract ITrigger Trigger { get; }
    public abstract ScheduleType ScheduleType { get; }
}


public class ScheduleEvery : ScheduleBase
{
    
    public ScheduleEvery(TimeSpan every)
    {
        Every = every;
    }
    private TimeSpan Every { get;}
    internal override ITrigger Trigger
    {
        get
        {
            return TriggerBuilder.Create()
                .WithSimpleSchedule(x => x. WithInterval(Every).RepeatForever())
                .Build();
        }
    }

    public override ScheduleType ScheduleType => ScheduleType.Every;
}