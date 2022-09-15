using Cronos;

namespace Rota.Scheduling;

/// <summary>
///     Defines a schedule determining when a job should be run.
/// </summary>
public abstract class Schedule
{
    private bool      _firstRun = true;
    private DateTime? _lastDueAt;
    private DateTime? _nextDueAt;
    private bool      _runImmediately;

    /// <summary>
    ///     The date and time at which this schedule was last due.
    ///     When accessed, this <see cref="DateTime" /> is converted to the timezone specified by <see cref="TimeZone" /> when
    ///     applicable.
    /// </summary>
    protected DateTime? LastDueAt => this._lastDueAt is null ? null : this.ConvertToZonedTime( this._lastDueAt.Value );

    /// <summary>
    ///     Allows the schedule to be timezone aware if a schedule is to be executed at a specific time relative to the local
    ///     timezone.
    ///     If not specified, UTC will be used.
    /// </summary>
    public TimeZoneInfo? TimeZone { get; set; }

    /// <summary>
    ///     Creates a new schedule using a cron expression.
    /// </summary>
    /// <param name="cronExpression">The cron expression to parse.</param>
    /// <param name="format">The format of the cron expression.</param>
    /// <returns>The schedule created from the specified cron expression.</returns>
    public static CronSchedule FromCron(
        string     cronExpression,
        CronFormat format = CronFormat.IncludeSeconds
    ) => new( CronExpression.Parse( cronExpression, format ) );

    /// <summary>
    ///     Creates a simple schedule that simply triggers after a specified delay.
    /// </summary>
    /// <param name="interval">The interval at which this schedule triggers.</param>
    /// <returns>The schedule constructed from the specified interval.</returns>
    public static IntervalSchedule FromInterval( TimeSpan interval ) => new( interval );

    /// <summary>
    ///     Indicates that this schedule allows the job it is attached to to run immediately after being registered with the
    ///     scheduler.
    ///     If not set, the schedule will not trigger until the first specified occurrence.
    /// </summary>
    /// <returns>The current schedule instance to allow for fluent configuration.</returns>
    public Schedule RunOnceAtStartup()
    {
        if( !this._firstRun ) return this;

        this._runImmediately = true;
        return this;
    }

    /// <summary>
    ///     Allows the schedule to be timezone aware if a schedule is to be executed at a specific time relative to the local
    ///     timezone.
    /// </summary>
    /// <param name="tzInfo">The specified timezone.</param>
    /// <returns>The current schedule instance to allow for fluent configuration.</returns>
    public Schedule ZonedTo( TimeZoneInfo tzInfo )
    {
        this.TimeZone = tzInfo;
        return this;
    }

    /// <summary>
    ///     Specifies that the current schedule is relative to the local timezone.
    /// </summary>
    /// <returns></returns>
    public Schedule ZonedToLocal() => this.ZonedTo( TimeZoneInfo.Local );

    /// <summary>
    ///     Specifies that the current schedule is relative to Universal Coordinated Time.
    /// </summary>
    /// <returns></returns>
    public Schedule ZonedToUtc() => this.ZonedTo( TimeZoneInfo.Utc );

    /// <summary>
    ///     Checks to see if this schedule is due to determine if a job should be executed.
    /// </summary>
    /// <param name="relativeStart">The current time to compare against.</param>
    /// <returns>
    ///     <see langword="true" /> if the schedule indicates the job is ready to execute, otherwise
    ///     <see langword="false" />.
    /// </returns>
    public virtual bool IsDue( DateTime relativeStart )
    {
        if( this._firstRun && this._runImmediately )
        {
            this._firstRun       = false;
            this._runImmediately = false;
            return true;
        }

        relativeStart   =   this.ConvertToZonedTime( relativeStart );
        this._nextDueAt ??= this.GetNextOccurrence( relativeStart );

        if( this._nextDueAt.Value > relativeStart ) return false;

        this._lastDueAt      = this._nextDueAt;
        this._firstRun       = false;
        this._runImmediately = false;
        this._nextDueAt      = null;

        return true;
    }

    /// <summary>
    ///     Converts <paramref name="dt" /> to this schedule's specified timezone, if present.
    /// </summary>
    /// <param name="dt">The date and time to adjust.</param>
    /// <returns>The adjusted date and time.</returns>
    protected DateTime ConvertToZonedTime( DateTime dt )
        => TimeZoneInfo.ConvertTime( dt, this.TimeZone ?? TimeZoneInfo.Utc );

    /// <summary>
    ///     Computes the date and time at which the schedule will next trigger.
    /// </summary>
    /// <param name="relativeStart">The current time to compare against.</param>
    /// <returns>The date and time at which the schedule will next trigger.</returns>
    protected abstract DateTime GetNextOccurrence( DateTime relativeStart );
}