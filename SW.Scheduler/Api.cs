namespace SW.PrimitiveTypes;
public interface IScheduledJobDefinition
{
    Type JobType { get; }
    Type JobParamsType { get; }
    bool WithParams { get; }
} 

public enum ScheduleType
{
    Every
}
public interface IScheduledJobBase
{
}

public interface IScheduledJobWithParams:IScheduledJobBase
{
}
public interface IScheduledJob:IScheduledJobBase
{
    Task Execute();
}

public interface IScheduledJob<TParam>:IScheduledJobWithParams
{
    Task Execute(TParam jobParams);
}


public interface ISchedule
{
    ScheduleType ScheduleType { get; }
}


public class SampleJob : IScheduledJob
{
    public Task Execute()
    {
        Console.WriteLine("SampleJob executed");
        return Task.CompletedTask;
    }
}

public class SampleJobParams
{
    public string CustomerGroup { get; set; }
}

public class SampleJobWithParams : IScheduledJob<SampleJobParams>
{
    
    public Task Execute(SampleJobParams jobParams)
    {
        Console.WriteLine($"SampleJobWithParams executed with param {jobParams.CustomerGroup}");
        return Task.CompletedTask;
    }
}

public interface IScheduleRepository
{
    public Task Schedule<Scheduler,TParam>(TParam param, string key, string cronExpression) where Scheduler : IScheduledJob<TParam>;
    public Task Schedule<Sheduler>(string cronExpression) where Sheduler : IScheduledJob;
    public Task ScheduleOnce(string name, string key,object jobParams, DateTime? runAt = null);
    public IEnumerable<IScheduledJobDefinition> GetBackgroundJobDefinitions();
    
}