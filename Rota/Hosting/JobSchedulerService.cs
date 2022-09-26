using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Rota.Hosting;

/// <summary>
///     Automatically runs the registered <see cref="JobScheduler" />.
/// </summary>
public sealed class JobSchedulerService : IHostedService, IDisposable
{
    private readonly IHostApplicationLifetime     _lifetime;
    private readonly ILogger<JobSchedulerService> _logger;
    private readonly JobScheduler                 _scheduler;
    private          bool                         _shouldRun;
    private          Timer?                       _timer;

    /// <summary>
    ///     Constructs a new instance of the service with the specified scheduler.
    /// </summary>
    /// <param name="logger">The host's logging service.</param>
    /// <param name="lifetime">The host's application lifetime.</param>
    /// <param name="scheduler">The scheduler to run.</param>
    public JobSchedulerService( ILogger<JobSchedulerService> logger, IHostApplicationLifetime lifetime,
                                JobScheduler                 scheduler )
    {
        this._logger    = logger;
        this._scheduler = scheduler;
        this._lifetime  = lifetime;
    }

    private bool IsActive => this._shouldRun || !this._scheduler.IsCancellationRequested || !this._scheduler.IsDisabled;

    /// <summary>
    ///     Performs final cleanup of the service.
    /// </summary>
    public void Dispose()
    {
        this._timer?.Dispose();
    }

    /// <summary>
    ///     Starts execution of the scheduler.
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A task that completes when the startup procedure has completed and the scheduler is running.</returns>
    public Task StartAsync( CancellationToken cancellationToken )
    {
        this._shouldRun = true;
        this._lifetime.ApplicationStarted.Register(
            () => {
                this._logger.LogTrace( "Initializing timer" );
                this._timer = new Timer( this.RunJobsAsync,
                                         null,
                                         TimeSpan.Zero, this._scheduler.Configuration.PollingRate
                );
            } );
        this._logger.LogInformation( "Scheduler service started" );

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Signals the scheduler to shutdown and cancel all currently running jobs.
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A task that completes when the scheduler has stopped and all active jobs have terminated.</returns>
    public async Task StopAsync( CancellationToken cancellationToken )
    {
        this._logger.LogInformation( "Scheduler service stopping" );
        this._timer?.Change( Timeout.InfiniteTimeSpan, TimeSpan.Zero );
        this._shouldRun            = false;
        this._scheduler.IsDisabled = true;
        this._scheduler.CancelAllJobs();

        await this._scheduler.WaitForAllJobsToExitAsync();
        this._logger.LogInformation( "Scheduler service stopped" );
    }

    private async void RunJobsAsync( object? _ )
    {
        if( !this.IsActive || this._scheduler.ActiveJobs > 0 ) return;
        await this._scheduler.RunJobsAsync();
        if( Debugger.IsAttached ) this._shouldRun = false;
    }
}
