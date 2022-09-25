using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace Rota.Jobs;

/// <summary>
///     A worker thread that manages the execution of individual jobs.
/// </summary>
public sealed class JobRunner
{
    private readonly JobSchedulerConfiguration _configuration;
    private          int                       _activeJobs;

    private ExecutionMode _executionMode;
    private ushort?       _maximumConcurrency;

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
    /// Gets an immutable collection containing the jobs registered to this worker thread.
    /// </summary>
    public ImmutableArray<ScheduledJob> Jobs => this.ScheduledJobs.ToImmutableArray();

    internal ConcurrentBag<ScheduledJob> ScheduledJobs { get; }

    internal JobRunner( string name, JobSchedulerConfiguration configuration )
    {
        this.Name                = name;
        this._executionMode      = configuration.JobRunnerExecutionMode;
        this._maximumConcurrency = configuration.JobRunnerMaximumConcurrency;
        this.IsDisabled          = false;
        this._configuration      = configuration;
        this.ScheduledJobs                = new ConcurrentBag<ScheduledJob>();
    }

    /// <summary>
    ///     Sets this thread to execute jobs concurrently.
    /// </summary>
    /// <param name="maximumConcurrency">
    ///     The maximum number of jobs that may be running at any given time.
    ///     If set to 0, concurrent execution will be unlimited.
    /// </param>
    /// <returns>The current job runner instance to allow for fluent configuration.</returns>
    public JobRunner Concurrently( ushort maximumConcurrency )
    {
        this._executionMode      = ExecutionMode.Concurrent;
        this._maximumConcurrency = maximumConcurrency;
        return this;
    }

    /// <summary>
    ///     Sets this thread to execute jobs consecutively.
    /// </summary>
    /// <returns>The current job runner instance to allow for fluent configuration.</returns>
    public JobRunner Consecutively()
    {
        this._executionMode      = ExecutionMode.Consecutive;
        this._maximumConcurrency = null;
        return this;
    }

    /// <summary>
    ///     Marks this job runner as enabled, allowing normal job execution.
    /// </summary>
    /// <returns>The current job runner instance to allow for fluent configuration.</returns>
    public JobRunner Enabled()
    {
        this.IsDisabled = false;
        return this;
    }

    /// <summary>
    ///     Marks this jop runner as disabled, disallowing all job executions.
    /// </summary>
    /// <returns>The current job runner instance to allow for fluent configuration.</returns>
    public JobRunner Disabled()
    {
        this.IsDisabled = true;
        return this;
    }

    internal async ValueTask ExecuteJobs( IServiceProvider? provider, CancellationToken cancellationToken )
    {
        if( this.IsDisabled || cancellationToken.IsCancellationRequested ) return;
        switch( this._executionMode )
        {
            case ExecutionMode.Concurrent:
            {
                var semaphore = new AsyncSemaphore(
                    this._maximumConcurrency is null or 0
                        ? Int64.MaxValue
                        : this._maximumConcurrency.Value
                );
                var tasks =
                    this.ScheduledJobs
                        .Select(
                             job => Task.Run(
                                 async () => {
                                     Thread.CurrentThread.Name = $"{this.Name} - {job.Id}";
                                     await semaphore.WaitAsync( cancellationToken );
                                     _ = Interlocked.Increment( ref this._activeJobs );
                                     await this.TryExecuteJob( job, provider, cancellationToken );
                                     semaphore.Release( 1 );
                                     _ = Interlocked.Decrement( ref this._activeJobs );
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
        try { await job.ExecuteAsync( provider, cancellationToken ); }
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
