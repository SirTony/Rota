using System.Threading.RateLimiting;
using Cronos;

namespace Rota.Scheduling;

/// <summary>
///     Defines a schedule determining when a job should be run.
/// </summary>
public abstract class Schedule
{
    private bool       _firstRun = true;
    private DateTime?  _lastDueAt;
    private DateTime?  _nextDueAt;
    private bool       _runImmediately;
    private Func<bool> _whenFunc;

    /// <summary>
    ///     The date and time at which this schedule will next trigger.
    /// </summary>
    public DateTime? NextDueAt => this._nextDueAt is null ? null : this.ConvertToZonedTime( this._nextDueAt.Value );

    /// <summary>
    ///     The date and time at which this schedule was last due.
    ///     When accessed, this <see cref="DateTime" /> is converted to the timezone specified by <see cref="TimeZone" /> when
    ///     applicable.
    /// </summary>
    public DateTime? LastDueAt => this._lastDueAt is null ? null : this.ConvertToZonedTime( this._lastDueAt.Value );

    /// <summary>
    ///     Allows the schedule to be timezone aware if a schedule is to be executed at a specific time relative to the local
    ///     timezone.
    ///     If not specified, UTC will be used.
    /// </summary>
    public TimeZoneInfo? TimeZone { get; set; }

    /// <summary>
    ///     Gets or sets whether this schedule is disabled.
    ///     When a scheduler is disabled it will never come due, disallowing all jobs that use the schedule from executing.
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    ///     Creates a new schedule using a cron expression.
    /// </summary>
    /// <param name="cronExpression">The cron expression to parse.</param>
    /// <param name="format">The format of the cron expression.</param>
    /// <returns>The schedule created from the specified cron expression.</returns>
    public static CronSchedule FromCron(
        string     cronExpression,
        CronFormat format = CronFormat.IncludeSeconds
    )
        => new( CronExpression.Parse( cronExpression, format ) );

    /// <summary>
    ///     Creates a simple schedule that simply triggers after a specified delay.
    /// </summary>
    /// <param name="interval">The interval at which this schedule triggers.</param>
    /// <returns>The schedule constructed from the specified interval.</returns>
    public static IntervalSchedule FromInterval( TimeSpan interval ) => new( interval );

    /// <summary>
    ///     Creates a new scheduler that wraps an existing schedule within a rate limiter.
    /// </summary>
    /// <param name="baseSchedule">The schedule to be rate limited.</param>
    /// <param name="rateLimiter">The rate limiter acting on <paramref name="baseSchedule" />.</param>
    /// <param name="debounceDuration">
    ///     The minimum amount of time between occurrences of <paramref name="baseSchedule" />
    ///     regardless of other rate limiting or schedule settings.
    /// </param>
    /// <returns>The schedule created from the specified base and rate limiter.</returns>
    public static RateLimitedSchedule WithRateLimiter(
        Schedule    baseSchedule,
        RateLimiter rateLimiter,
        TimeSpan    debounceDuration = default
    )
        => new( baseSchedule, rateLimiter, debounceDuration );

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
    /// <param name="timeZoneInfo">The specified timezone.</param>
    /// <returns>The current schedule instance to allow for fluent configuration.</returns>
    public Schedule ZonedTo( TimeZoneInfo timeZoneInfo )
    {
        this.TimeZone = timeZoneInfo;
        return this;
    }

    /// <summary>
    ///     Specifies that the current schedule is relative to the local timezone.
    /// </summary>
    /// <returns>The current schedule instance to allow for fluent configuration.</returns>
    public Schedule ZonedToLocal() => this.ZonedTo( TimeZoneInfo.Local );

    /// <summary>
    ///     Specifies that the current schedule is relative to Universal Coordinated Time.
    /// </summary>
    /// <returns>The current schedule instance to allow for fluent configuration.</returns>
    public Schedule ZonedToUtc() => this.ZonedTo( TimeZoneInfo.Utc );

    /// <summary>
    ///     Specifies a <see langword="delegate" /> that contains conditional logic determining whether or not this schedule
    ///     should be run at its next occurrence.
    /// </summary>
    /// <param name="condition">
    ///     A <see langword="delegate" /> that contains logic to determine whether or not the schedule
    ///     should run at its next occurrence.
    ///     If the function returns <see langword="true" /> the schedule will trigger, if it returns <see langword="false" />
    ///     the schedule
    ///     will not trigger even if the underlying temporal logic indicates the due time has passed.
    /// </param>
    /// <returns>The current schedule instance to allow for fluent configuration.</returns>
    public virtual Schedule When( Func<bool> condition )
    {
        this._whenFunc = condition;
        return this;
    }

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
        if( this.IsDisabled ) return false;
        if( this._firstRun && this._runImmediately )
        {
            this._firstRun       = false;
            this._runImmediately = false;
            return true;
        }

        relativeStart = this.ConvertToZonedTime( relativeStart );

        this._nextDueAt ??= this.GetNextOccurrence( relativeStart );

        if( this._nextDueAt.Value >= relativeStart ) return false;
        if( this._whenFunc?.Invoke() is true ) return true;

        this._lastDueAt      = this.NextDueAt;
        this._firstRun       = false;
        this._runImmediately = false;
        this._nextDueAt      = null;

        return true;
    }

    /// <summary>
    ///     Converts <paramref name="dateTime" /> to this schedule's specified timezone, if present.
    /// </summary>
    /// <param name="dateTime">The date and time to adjust.</param>
    /// <returns>The adjusted date and time.</returns>
    protected DateTime ConvertToZonedTime( DateTime dateTime )
        => TimeZoneInfo.ConvertTime( dateTime, this.TimeZone ?? TimeZoneInfo.Utc );

    /// <summary>
    ///     Computes the date and time at which the schedule will next trigger.
    ///     Depending on the implementation, repeated calls to this method could result in the schedule skipping ahead
    ///     and missing one or more of it's scheduled occurrences.
    /// </summary>
    /// <param name="relativeStart">The current time to compare against.</param>
    /// <returns>The date and time at which the schedule will next trigger.</returns>
    public abstract DateTime GetNextOccurrence( DateTime relativeStart );
}
