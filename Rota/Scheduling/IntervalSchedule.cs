namespace Rota.Scheduling;

/// <summary>
///     Specifies a simple schedule that executes at a set interval.
/// </summary>
public class IntervalSchedule : Schedule
{
    /// <summary>
    ///     Constructs a new schedule with the specified interval.
    /// </summary>
    /// <param name="interval">The delay between each execution of this schedule.</param>
    public IntervalSchedule( TimeSpan interval ) => this.Interval = interval;

    /// <summary>
    ///     The interval at which this schedule triggers.
    /// </summary>
    public TimeSpan Interval { get; }

    /// <inheritdoc />
    public override DateTime GetNextOccurrence( DateTime relativeStart ) =>
        this.LastDueAt is not null
            ? this.LastDueAt.Value
            + this.Interval
            : this.ConvertToZonedTime( relativeStart )
            + this.Interval;
}
