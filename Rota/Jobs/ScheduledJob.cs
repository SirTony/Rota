using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rota.Scheduling;

namespace Rota.Jobs;

/// <summary>
///     Represents a job that is managed by the scheduler.
/// </summary>
public sealed class ScheduledJob
{
    private readonly JobSchedulerConfiguration _configuration;
    private readonly ImmutableArray<object?>   _constructorArguments;
    private readonly Type                      _jobType;

    internal ScheduledJob(
        Guid                      id,
        Schedule                  schedule,
        Type                      jobType,
        IEnumerable<object?>      ctorArgs,
        JobSchedulerConfiguration configuration
    )
    {
        this.Id                    = id;
        this.Schedule              = schedule;
        this.Schedule              = schedule;
        this._jobType              = jobType;
        this._configuration        = configuration;
        this._constructorArguments = ImmutableArray.CreateRange( ctorArgs );
    }

    /// <summary>
    ///     A unique identifier for this job.
    ///     This identifier is unique across all jobs of the same underlying type across all worker threads.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    ///     The <see cref="Rota.Scheduling.Schedule" /> that determines when this job executes.
    /// </summary>
    public Schedule Schedule { get; }

    /// <summary>
    ///     Determines whether or not this job has been disabled.
    ///     By default, this property is set to <see langword="false" />, but may be set to <see langword="true" />
    ///     internally if this job throws an exception during execution if
    ///     <see cref="JobSchedulerConfiguration.ErrorHandlingStrategy" /> s set to
    ///     <see cref="ErrorHandlingStrategy.DisableJob" />.
    ///     It is possible to re-enable this job programatically although it will just disable itself again
    ///     when it throws an exception unless graceful error handling is implemented.
    /// </summary>
    public bool IsDisabled { get; set; }

    internal async ValueTask ExecuteAsync( IServiceProvider? provider, CancellationToken cancellationToken )
    {
        if( this.IsDisabled || cancellationToken.IsCancellationRequested || !this.Schedule.IsDue( DateTime.UtcNow ) )
            return;

        var lease = this.Schedule is RateLimitedSchedule rateLimitedSchedule
            ? await rateLimitedSchedule.RateLimiter.AcquireAsync( 1, cancellationToken )
            : null;

        if( lease?.IsAcquired is false ) return;

        await using var scope    = provider?.CreateAsyncScope();
        
        var       ctorArgs = this._constructorArguments.ToArray();
        var jobInstance = scope.HasValue
            ? ActivatorUtilities.CreateInstance( scope.Value.ServiceProvider, this._jobType, ctorArgs! )
            : Activator.CreateInstance( this._jobType,
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                        null,
                                        ctorArgs,
                                        CultureInfo.CurrentCulture
            );

        var logger = scope?.ServiceProvider.GetService<ILogger<ScheduledJob>>();
        if( jobInstance is null or not IJob )
        {
            logger?.LogTrace( "failed to activate scheduled job, aborting execution" );
            return;
        }

        var job = (IJob)jobInstance;
        var fullJobName = String.IsNullOrWhiteSpace( job.Name )
            ? $"{this._jobType.FullName}::{this.Id}"
            : $"{this._jobType.FullName}::{this.Id} ({job.Name})";

        if( !String.IsNullOrWhiteSpace( job.Name ) ) Thread.CurrentThread.Name += $" [{job.Name}]";

        logger?.LogTrace( "executing job {FullJobName}", fullJobName );
        try
        {
            await job.ExecuteAsync( cancellationToken );
        }
        catch( TaskCanceledException )
        {
        }
        catch( Exception ) when( this._configuration.ErrorHandlingStrategy is ErrorHandlingStrategy.Ignore )
        {
        }
        catch( Exception ex )
            when( this._configuration.ErrorHandlingStrategy is ErrorHandlingStrategy.DisableJob )
        {
            this.IsDisabled = true;
            logger?.LogError(
                "{FullJobName} has encountered an exception and has been permanently disabled",
                fullJobName
            );
            this._configuration.ErrorHandler?.Invoke( ex );
        }
        finally
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if( job is IAsyncDisposable asyncDisposable )
            {
                logger?.LogTrace( "disposing job {Id} asynchronously", this );
                await asyncDisposable.DisposeAsync();
            }

            // ReSharper disable once SuspiciousTypeConversion.Global
            if( job is IDisposable disposable )
            {
                logger?.LogTrace( "disposing job {Id} synchronously", this );
                disposable.Dispose();
            }

            lease?.Dispose();
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{this._jobType.FullName}::{this.Id}";
}
