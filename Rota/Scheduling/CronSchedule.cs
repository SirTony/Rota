using Cronos;

namespace Rota.Scheduling;

/// <summary>
///     A schedule defined by a cron expression.
/// </summary>
public class CronSchedule : Schedule
{
    private readonly CronExpression _cron;

    /// <summary>
    ///     Constructs a new schedule using a cron expression.
    /// </summary>
    /// <param name="cron">The cron expression of the schedule.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cron" /> is <see langword="null" />.</exception>
    public CronSchedule( CronExpression cron )
        => this._cron = cron ?? throw new ArgumentNullException( nameof( cron ) );

    /// <inheritdoc />
    public override DateTime GetNextOccurrence( DateTime relativeStart )
        => this._cron.GetNextOccurrence( relativeStart, this.TimeZone ?? TimeZoneInfo.Utc, true ) ?? DateTime.MaxValue;
}
