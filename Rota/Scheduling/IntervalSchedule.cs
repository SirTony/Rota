namespace Rota.Scheduling;

public class IntervalSchedule : Schedule
{
    public TimeSpan Interval { get; }

    public IntervalSchedule( TimeSpan interval ) => this.Interval = interval;

    protected override DateTime GetNextOccurrence( DateTime relativeStart ) => this.LastDueAt is not null
                                                                                   ? this.LastDueAt.Value
                                                                                     + this.Interval
                                                                                   : this.ConvertToZonedTime(
                                                                                           relativeStart
                                                                                       )
                                                                                     + this.Interval;
}