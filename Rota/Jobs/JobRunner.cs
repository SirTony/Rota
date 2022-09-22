using System.Collections.Concurrent;
using Nito.AsyncEx;

namespace Rota.Jobs;

/// <summary>
///     A worker thread that manages the execution of individual jobs.
/// </summary>
public sealed class JobRunner
{
    private int _activeJobs;

    /// <summary>
    ///     The current number of jobs active on this thread.
    /// </summary>
    public int ActiveJobs => this._activeJobs;

    /// <summary>
    ///     The worker thread's name.
    /// </summary>
    public string Name { get; }

    internal ExecutionMode               ExecutionMode      { get; set; }
    internal ushort?                     MaximumConcurrency { get; set; }
    internal ConcurrentBag<ScheduledJob> Jobs               { get; }

    internal JobRunner( string name, ExecutionMode executionMode, ushort? maximumConcurrency )
    {
        this.Name               = name;
        this.ExecutionMode      = executionMode;
        this.MaximumConcurrency = maximumConcurrency;
        this.Jobs               = new ConcurrentBag<ScheduledJob>();
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
        this.ExecutionMode      = ExecutionMode.Concurrent;
        this.MaximumConcurrency = maximumConcurrency;
        return this;
    }

    /// <summary>
    ///     Sets this thread to execute jobs consecutively.
    /// </summary>
    /// <returns>The current job runner instance to allow for fluent configuration.</returns>
    public JobRunner Consecutively()
    {
        this.ExecutionMode      = ExecutionMode.Consecutive;
        this.MaximumConcurrency = null;
        return this;
    }

    internal async ValueTask ExecuteJobs( IServiceProvider? provider, CancellationToken cancellationToken )
    {
        if( cancellationToken.IsCancellationRequested ) return;
        switch( this.ExecutionMode )
        {
            case ExecutionMode.Concurrent:
            {
                var semaphore = new AsyncSemaphore(
                    this.MaximumConcurrency is null or 0
                        ? Int64.MaxValue
                        : this.MaximumConcurrency.Value
                );
                var tasks =
                    this.Jobs
                        .Select(
                             job => Task.Run(
                                 async () => {
                                     Thread.CurrentThread.Name = $"{this.Name} - {job.Id}";
                                     await semaphore.WaitAsync( cancellationToken );
                                     _ = Interlocked.Increment( ref this._activeJobs );
                                     await job.ExecuteAsync( provider, cancellationToken );
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
                foreach( var job in this.Jobs )
                {
                    await Task.Run(
                        async () => {
                            Thread.CurrentThread.Name = $"{this.Name} - {job.Id}";
                            _                         = Interlocked.Increment( ref this._activeJobs );
                            await job.ExecuteAsync( provider, cancellationToken );
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
}
