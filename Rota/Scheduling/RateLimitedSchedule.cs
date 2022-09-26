using System.Threading.RateLimiting;

namespace Rota.Scheduling;

/// <summary>
///     A schedule that wraps an existing schedule and applies rate limiting logic to its occurrences.
/// </summary>
public sealed class RateLimitedSchedule : Schedule
{
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
        this.BaseSchedule     = baseSchedule ?? throw new ArgumentNullException( nameof( baseSchedule ) );
        this.RateLimiter      = rateLimiter  ?? throw new ArgumentNullException( nameof( rateLimiter ) );
        this.DebounceDuration = debounceDuration;
    }

    /// <summary>
    ///     The underlying schedule that determines when this schedule is due.
    /// </summary>
    public Schedule BaseSchedule { get; }

    /// <summary>
    ///     The minimum duration between occurrences of this schedule.
    ///     Even if <see cref="BaseSchedule" /> triggers more frequently than this duration,
    ///     it will still be limited by this property.
    /// </summary>
    public TimeSpan DebounceDuration { get; set; }

    /// <summary>
    ///     The rate limiter that further restricts when <see cref="BaseSchedule" /> triggers.
    /// </summary>
    public RateLimiter RateLimiter { get; }

    /// <inheritdoc />
    public override bool IsDue( DateTime relativeStart )
    {
        var diff = this.LastDueAt - this.ConvertToZonedTime( relativeStart ) ?? TimeSpan.Zero;
        return diff <= this.DebounceDuration && base.IsDue( relativeStart );
    }

    /// <inheritdoc />
    public override DateTime GetNextOccurrence( DateTime relativeStart )
        => this.BaseSchedule.GetNextOccurrence( relativeStart );
}
