using Cronos;

namespace Rota.Scheduling;

public class CronSchedule : Schedule
{
    private readonly CronExpression _cron;

    public CronSchedule( CronExpression cron )
        => this._cron = cron ?? throw new ArgumentNullException( nameof( cron ) );

    protected override DateTime GetNextOccurrence( DateTime relativeStart )
        => this._cron.GetNextOccurrence( relativeStart, this.TimeZone ?? TimeZoneInfo.Utc, true ) ?? DateTime.MaxValue;
}