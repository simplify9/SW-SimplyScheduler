namespace SW.Scheduler;

/// <summary>
/// Defines what happens when a trigger misfires (i.e. its scheduled time was missed).
/// </summary>
public enum MisfireInstructions
{
    /// <summary>Fire the job once as soon as the scheduler notices the misfire, then resume normal schedule.</summary>
    FireOnce,

    /// <summary>Skip all misfired executions and wait for the next scheduled time.</summary>
    Skip,

    /// <summary>Fire once for every misfired execution that was missed.</summary>
    FireAll
}

