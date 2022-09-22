using System.Threading.RateLimiting;

namespace Rota.Scheduling;

/// <summary>
///     A schedule that wraps an existing schedule and applies rate limiting logic to its occurrences.
/// </summary>
public sealed class RateLimitedSchedule : Schedule
{
    private readonly Schedule    _baseSchedule;
    private readonly TimeSpan    _debounce;
    private readonly RateLimiter _rateLimiter;

    /// <summary>
    ///     Creates a new scheduler that wraps an existing schedule within a rate limiter.
    /// </summary>
    /// <param name="baseSchedule">The schedule to be rate limited.</param>
    /// <param name="rateLimiter">The rate limiter acting on <paramref name="baseSchedule" />.</param>
    /// <param name="debounceDuration">
    ///     The minimum amount of time between occurrences of <paramref name="baseSchedule" />
    ///     regardless of other rate limiting or schedule settings.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when either <paramref name="baseSchedule" /> or
    ///     <paramref name="rateLimiter" /> are <see langword="null" />.
    /// </exception>
    public RateLimitedSchedule( Schedule baseSchedule, RateLimiter rateLimiter, TimeSpan debounceDuration = default )
    {
        this._baseSchedule = baseSchedule ?? throw new ArgumentNullException( nameof( baseSchedule ) );
        this._rateLimiter  = rateLimiter  ?? throw new ArgumentNullException( nameof( rateLimiter ) );
        this._debounce     = debounceDuration;
    }

    /// <inheritdoc />
    public override bool IsDue( DateTime relativeStart )
    {
        if( this.ConvertToZonedTime( relativeStart ) - this.LastDueAt < this._debounce ) return false;

        using var lease = this._rateLimiter.AttemptAcquire();
        return lease.IsAcquired && base.IsDue( relativeStart );
    }

    /// <inheritdoc />
    public override DateTime GetNextOccurrence( DateTime relativeStart )
        => this._baseSchedule.GetNextOccurrence( relativeStart );
}
