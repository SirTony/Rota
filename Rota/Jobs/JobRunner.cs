using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Rota.Jobs;

/// <summary>
///     A worker thread that manages the execution of individual jobs.
/// </summary>
public sealed class JobRunner
{
    private readonly RateLimiter               _concurrencyLimiter;
    private readonly JobSchedulerConfiguration _configuration;
    private          int                       _activeJobs;

    internal JobRunner( string name, JobSchedulerConfiguration configuration )
    {
        this.Name           = name;
        this.IsDisabled     = false;
        this._configuration = configuration;
        this.ScheduledJobs  = new ConcurrentBag<ScheduledJob>();

        var concurrencyLimit = this._configuration.JobRunnerMaximumConcurrency switch
        {
            null  => Environment.ProcessorCount * 2,
            0     => Int32.MaxValue,
            var x => x.Value,
        };

        var limiterOptions = new ConcurrencyLimiterOptions
        {
            PermitLimit          = concurrencyLimit,
            QueueLimit           = concurrencyLimit * 2,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        };

        this._concurrencyLimiter = new ConcurrencyLimiter( limiterOptions );
    }

    /// <summary>
    ///     The current number of jobs active on this thread.
    /// </summary>
    public int ActiveJobs => this._activeJobs;

    /// <summary>
    ///     The worker thread's name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Determines whether or not this job runner has been disabled.
    ///     By default, this property is set to <see langword="false" />, but may be set to <see langword="true" />
    ///     internally if one of the jobs throws an exception during execution if
    ///     <see cref="JobSchedulerConfiguration.ErrorHandlingStrategy" /> s set to
    ///     <see cref="ErrorHandlingStrategy.DisableWorker" />.
    ///     It is possible to re-enable this worker programatically although it will just disable itself again
    ///     when another job throws an exception unless graceful error handling is implemented within the problematic job.
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    ///     Gets an immutable collection containing the jobs registered to this worker thread.
    /// </summary>
    public ImmutableArray<ScheduledJob> Jobs => this.ScheduledJobs.ToImmutableArray();

    internal ConcurrentBag<ScheduledJob> ScheduledJobs { get; }

    internal async ValueTask ExecuteJobs( IServiceProvider? provider, CancellationToken cancellationToken )
    {
        if( this.IsDisabled || cancellationToken.IsCancellationRequested ) return;
        switch( this._configuration.JobRunnerExecutionMode )
        {
            case ExecutionMode.Concurrent:
            {
                var tasks = this.ScheduledJobs
                                .Select(
                                     job => Task.Run(
                                         async () => {
                                             Thread.CurrentThread.Name = $"{this.Name} - {job.Id}";
                                             var lease = await this._concurrencyLimiter.AcquireAsync(
                                                 1, cancellationToken );
                                             if( !lease.IsAcquired ) return;

                                             _ = Interlocked.Increment( ref this._activeJobs );
                                             await this.TryExecuteJob( job, provider, cancellationToken );
                                             _ = Interlocked.Decrement( ref this._activeJobs );
                                             lease.Dispose();
                                         },
                                         cancellationToken
                                     )
                                 )
                                .ToList();

                await Task.WhenAll( tasks );
                break;
            }

            case ExecutionMode.Consecutive:
            {
                foreach( var job in this.ScheduledJobs )
                {
                    await Task.Run(
                        async () => {
                            Thread.CurrentThread.Name = $"{this.Name} - {job.Id}";
                            _                         = Interlocked.Increment( ref this._activeJobs );
                            await this.TryExecuteJob( job, provider, cancellationToken );
                            _ = Interlocked.Decrement( ref this._activeJobs );
                        },
                        cancellationToken
                    );
                }

                break;
            }

            default: throw new NotSupportedException();
        }

        Thread.CurrentThread.Name = this.Name;
        _                         = Interlocked.Exchange( ref this._activeJobs, 0 );
    }

    private async ValueTask TryExecuteJob(
        ScheduledJob      job,
        IServiceProvider? provider,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await job.ExecuteAsync( provider, cancellationToken );
        }
        catch( TaskCanceledException )
        {
        }
        catch( Exception ) when( this._configuration.ErrorHandlingStrategy is ErrorHandlingStrategy.Ignore )
        {
        }
        catch( Exception ex ) when( this._configuration.ErrorHandlingStrategy is ErrorHandlingStrategy.DisableWorker )
        {
            this.IsDisabled = true;
            provider?.GetService<ILogger<JobRunner>>()
                    ?.LogError(
                          "Worker thread {Name} encountered an error while executing job {JobName} and has been permanently disabled",
                          this.Name,
                          job
                      );
            this._configuration.ErrorHandler?.Invoke( ex );
        }
    }
}
