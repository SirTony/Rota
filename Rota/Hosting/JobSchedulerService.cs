using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Rota.Hosting;

/// <summary>
///     Automatically runs the registered <see cref="JobScheduler" />.
/// </summary>
public sealed class JobSchedulerService : IHostedService, IDisposable
{
    private readonly ILogger<JobSchedulerService> _logger;
    private readonly JobScheduler                 _scheduler;
    private readonly Timer                        _timer;
    private          bool                         _shouldRun;

    /// <summary>
    ///     Constructs a new instance of the service with the specified scheduler.
    /// </summary>
    /// <param name="logger">The host's logging service.</param>
    /// <param name="scheduler">The scheduler to run.</param>
    public JobSchedulerService( ILogger<JobSchedulerService> logger, JobScheduler scheduler )
    {
        this._logger    = logger;
        this._scheduler = scheduler;
        this._timer     = new Timer( this.Tick, null, TimeSpan.Zero, this._scheduler.Configuration.PollingRate );
    }

    private async void Tick( object? state )
    {
        if( !this._shouldRun ) return;
        await this._scheduler.RunJobsAsync();
    }

    /// <summary>
    ///     Performs final cleanup of the service.
    /// </summary>
    public void Dispose() => this._timer.Dispose();

    /// <summary>
    ///     Starts execution of the scheduler.
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A task that completes when the startup procedure has completed and the scheduler is running.</returns>
    public Task StartAsync( CancellationToken cancellationToken )
    {
        this._shouldRun = true;
        this._logger.LogInformation( "Scheduler service started." );

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Signals the scheduler to shutdown and cancel all currently running jobs.
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A task that completes when the scheduler has stopped and all active jobs have terminated.</returns>
    public async Task StopAsync( CancellationToken cancellationToken )
    {
        this._logger.LogInformation( "Scheduler service stopping." );
        this._shouldRun = false;
        this._scheduler.CancelAllJobs();

        await this._scheduler.WaitForAllJobsToExitAsync();
        this._logger.LogInformation( "Scheduler service stopped." );
    }
}
