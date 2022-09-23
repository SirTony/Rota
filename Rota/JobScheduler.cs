using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using Rota.Jobs;
using Rota.Scheduling;

namespace Rota;

/// <summary>
///     Coordinates the execution of worker threads to execute jobs registered with instances of this
///     <see langword="class" />.
/// </summary>
public sealed class JobScheduler
{
    private readonly CancellationTokenSource                 _cancellationTokenSource;
    private readonly IServiceProvider?                       _provider;
    private readonly ConcurrentDictionary<string, JobRunner> _workers;

    /// <summary>
    ///     Determines whether or not the scheduler has been disabled.
    ///     By default, this property is set to <see langword="false" />, but may be set to <see langword="true" />
    ///     internally if one of the job throw an exception during execution if
    ///     <see cref="JobSchedulerConfiguration.ErrorHandlingStrategy" /> s set to
    ///     <see cref="ErrorHandlingStrategy.StopScheduler" />.
    ///     It is possible to re-enable the scheduler programatically although it will just disable itself again
    ///     when another job or job runner throws an exception unless graceful error handling is implemented.
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    ///     The scheduler's current configuration.
    /// </summary>
    public JobSchedulerConfiguration Configuration { get; }

    /// <summary>
    ///     The total number of active jobs in all active worker threads.
    /// </summary>
    public int ActiveJobs => this._workers.Sum( pair => pair.Value.ActiveJobs );

    /// <summary>
    ///     <see langword="true" /> if cancellation has been requested by <see cref="CancelAllJobs" />, otherwise
    ///     <see langword="false" />
    /// </summary>
    public bool IsCancellationRequested => this._cancellationTokenSource.IsCancellationRequested;

    /// <summary>
    ///     Exposes the scheduler's internal cancellation token that is used to signal cancellation to all job runner and job
    ///     threads
    ///     to allow external synchronization with the scheduler.
    /// </summary>
    public CancellationToken CancellationToken => this._cancellationTokenSource.Token;

    /// <summary>
    ///     Constructs a new job scheduler using the specified configuration.
    /// </summary>
    /// <param name="config">
    ///     The configuration object defining the behaviour of this instance.
    ///     When not specified, <see cref="JobSchedulerConfiguration.Default" /> is used.
    /// </param>
    /// <param name="provider">
    ///     The dependency injection service container to use for resolving job dependencies when running in a hosted context.
    ///     This will be automamagically registered by the host when running in a hosted context.
    /// </param>
    public JobScheduler( JobSchedulerConfiguration? config = null, IServiceProvider? provider = null )
    {
        this.IsDisabled               = false;
        this._cancellationTokenSource = new CancellationTokenSource();
        this.Configuration            = config ?? JobSchedulerConfiguration.Default;
        this._provider                = provider;
        this._workers                 = new ConcurrentDictionary<string, JobRunner>();
    }

    /// <summary>
    ///     Configures a worker thread for the specified job type.
    ///     Even if no jobs of type <typeparamref name="T" /> have been registered yet, the worker thread may still be
    ///     configured ahead of time.
    /// </summary>
    /// <typeparam name="T">The job type used to lookup the worker thread.</typeparam>
    /// <param name="config">A <see langword="delegate" /> used to configure the worker thread.</param>
    public void ConfigureJobRunner<T>( Action<JobRunner> config )
        where T : IJob
        => this.ConfigureJobRunner( typeof( T ).FullName!, config );

    /// <summary>
    ///     Configures a worker thread for the specified job type.
    ///     Even if no jobs have been registered yet to the specified worker yet, the worker thread may still be configured
    ///     ahead of time.
    /// </summary>
    /// <param name="workerName">The name of the worker thread.</param>
    /// <param name="config">A <see langword="delegate" /> used to configure the worker thread.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config" /> is <see langword="null" /></exception>
    public void ConfigureJobRunner( string workerName, Action<JobRunner> config )
    {
        if( String.IsNullOrWhiteSpace( workerName ) )
        {
            throw new ArgumentNullException(
                nameof( workerName ),
                "Worker name cannot be null, empty, or consist entirely of whitespace"
            );
        }

        if( config is null ) throw new ArgumentNullException( nameof( config ) );

        if( this._workers.TryGetValue( workerName, out var worker ) )
            config( worker );
        else
        {
            worker = new JobRunner(
                workerName,
                this.Configuration
            );

            this._workers[workerName] = worker;
            config( worker );
        }
    }

    /// <summary>
    ///     Registers a job on the scheduler.
    ///     All jobs of the same type <typeparamref name="T" /> will be registered on the same worker thread.
    /// </summary>
    /// <typeparam name="T">The job type to register.</typeparam>
    /// <param name="schedule">The schedule defining when the job should be executed.</param>
    /// <param name="constructorArguments">Optional arguments to pass to the job's constructor when executing the job.</param>
    /// <returns>The current scheduler instance to support fluent registrations.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="schedule" /> is <see langword="null" />.</exception>
    public JobScheduler ScheduleJob<T>(
        Schedule         schedule,
        params object?[] constructorArguments
    )
        where T : IJob
        => this.ScheduleJobOnWorkerImpl<T>( null, schedule, constructorArguments );

    /// <summary>
    ///     Registers a job on the scheduler on the specified worker thread.
    ///     If <paramref name="workerName" /> is <see langword="null" />, the job will be registered to the default worker
    ///     for the the job type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The job type to register.</typeparam>
    /// <param name="workerName">The name of the worker thread to register the job with.</param>
    /// <param name="schedule">The schedule defining when the job should be executed.</param>
    /// <param name="constructorArguments">Optional arguments to pass to the job's constructor when executing the job.</param>
    /// <returns>The current scheduler instance to support fluent registrations.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="schedule" /> is <see langword="null" />.</exception>
    public JobScheduler ScheduleJobOnWorker<T>(
        string           workerName,
        Schedule         schedule,
        params object?[] constructorArguments
    )
        where T : IJob
        => this.ScheduleJobOnWorkerImpl<T>(
            workerName,
            schedule,
            constructorArguments
        );

    private JobScheduler ScheduleJobOnWorkerImpl<T>(
        string?               workerName,
        Schedule              schedule,
        IEnumerable<object?>? constructorArguments
    )
        where T : IJob
    {
        if( schedule is null ) throw new ArgumentNullException( nameof( schedule ) );

        var job = new ScheduledJob(
            Guid.NewGuid(),
            typeof( T ),
            schedule,
            this.Configuration,
            ImmutableArray.CreateRange( constructorArguments ?? Enumerable.Empty<object?>() )
        );

        workerName = String.IsNullOrWhiteSpace( workerName ) ? typeof( T ).FullName! : workerName;
        if( this._workers.TryGetValue( workerName, out var worker ) )
            worker.Jobs.Add( job );
        else
        {
            this._workers[workerName] = new JobRunner(
                workerName,
                this.Configuration
            );

            this._workers[workerName].Jobs.Add( job );
        }

        return this;
    }

    /// <summary>
    ///     Asynchronously runs all registered worker threads according to the execution strategy specified in
    ///     <see cref="Configuration" />.
    /// </summary>
    /// <returns>A task that completes when all jobs have completed.</returns>
    /// <exception cref="NotSupportedException">Thrown when an invalid <see cref="ExecutionMode" /> is specified.</exception>
    public async ValueTask RunJobsAsync()
    {
        if( this._cancellationTokenSource.IsCancellationRequested || this.IsDisabled ) return;
        switch( this.Configuration.JobRunnerExecutionMode )
        {
            case ExecutionMode.Concurrent:
            {
                var semaphore = new AsyncSemaphore(
                    this.Configuration.JobRunnerMaximumConcurrency is null or 0
                        ? Int64.MaxValue
                        : this.Configuration.JobRunnerMaximumConcurrency.Value
                );

                var tasks =
                    this._workers
                        .Select(
                             pair => Task.Run(
                                 async () => {
                                     Thread.CurrentThread.Name = pair.Key;
                                     await semaphore.WaitAsync( this._cancellationTokenSource.Token );
                                     await this.TryRunWorkerThread(
                                         pair.Value,
                                         this._provider,
                                         this._cancellationTokenSource.Token
                                     );
                                     semaphore.Release( 1 );
                                 },
                                 this._cancellationTokenSource.Token
                             )
                         )
                        .ToList();

                await Task.WhenAll( tasks );
                break;
            }

            case ExecutionMode.Consecutive:
            {
                foreach( var (name, worker) in this._workers )
                {
                    await Task.Run(
                        async () => {
                            Thread.CurrentThread.Name = name;
                            await this.TryRunWorkerThread(
                                worker,
                                this._provider,
                                this._cancellationTokenSource.Token
                            );
                        },
                        this._cancellationTokenSource.Token
                    );
                }

                break;
            }

            default: throw new NotSupportedException();
        }

        Thread.CurrentThread.Name = null;
    }

    /// <summary>
    ///     Attempts a graceful shutdown of all currently running jobs by signaling cancellation of
    ///     a <see cref="CancellationToken" />, but does not kill running jobs by force.
    /// </summary>
    public void CancelAllJobs()
    {
        if( !this._cancellationTokenSource.IsCancellationRequested ) this._cancellationTokenSource.Cancel( true );
    }

    /// <summary>
    ///     Returns a task that completes when all active jobs have exited after a cancellation.
    ///     This task will never complete unless a cancellation has been requested with <see cref="CancelAllJobs" />.
    /// </summary>
    /// <returns></returns>
    public async ValueTask WaitForAllJobsToExitAsync()
    {
        while( this.ActiveJobs > 0 && !this._cancellationTokenSource.IsCancellationRequested )
            await Task.Delay( TimeSpan.FromMilliseconds( 50 ), CancellationToken.None );
    }

    private async ValueTask TryRunWorkerThread(
        JobRunner         runner,
        IServiceProvider? provider,
        CancellationToken cancellationToken
    )
    {
        try { await runner.ExecuteJobs( provider, cancellationToken ); }
        catch( Exception ex ) when( this.Configuration.ErrorHandlingStrategy is ErrorHandlingStrategy.StopScheduler )
        {
            this.IsDisabled = true;
            provider?.GetService<ILogger<JobScheduler>>()
                    ?.LogError( "Terminating scheduler due to an excception in worker thread {Name}", runner.Name );
            this.Configuration.ErrorHandler?.Invoke( ex );
        }
    }
}
