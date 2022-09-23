using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rota.Scheduling;

namespace Rota.Jobs;

internal record struct ScheduledJob(
    Guid                      Id,
    Type                      JobType,
    Schedule                  Schedule,
    JobSchedulerConfiguration Configuration,
    ImmutableArray<object?>   ConstructorArguments
)
{
    private bool _isDisabled = false;

    public async ValueTask ExecuteAsync( IServiceProvider? provider, CancellationToken cancellationToken )
    {
        if( this._isDisabled || cancellationToken.IsCancellationRequested || !this.Schedule.IsDue( DateTime.UtcNow ) )
            return;

        var ctorArgs = this.ConstructorArguments.ToArray();
        var jobInstance = provider is not null
            ? ActivatorUtilities.CreateInstance( provider, this.JobType, ctorArgs! )
            : Activator.CreateInstance(
                this.JobType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                ctorArgs,
                CultureInfo.CurrentCulture
            );

        var logger = provider?.GetService<ILogger<ScheduledJob>>();
        if( jobInstance is null or not IJob )
        {
            logger?.LogTrace( "failed to activate scheduled job, aborting execution" );
            return;
        }

        var job = (IJob)jobInstance;
        var fullJobName = String.IsNullOrWhiteSpace( job.Name )
            ? $"{this.JobType.FullName}::{this.Id}"
            : $"{this.JobType.FullName}::{this.Id} ({job.Name})";

        if( !String.IsNullOrWhiteSpace( job.Name ) ) Thread.CurrentThread.Name += $" [{job.Name}]";

        logger?.LogTrace( "executing job {FullJobName}", fullJobName );
        try { await job.ExecuteAsync( cancellationToken ); }
        catch( Exception ex )
            when( this.Configuration.ErrorHandlingStrategy is ErrorHandlingStrategy.DisableJob )
        {
            this._isDisabled = true;
            logger?.LogError(
                "{FullJobName} has encountered an exception and has been permanently disabled",
                fullJobName
            );
            this.Configuration?.ErrorHandler?.Invoke( ex );
        }
        finally
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if( job is IAsyncDisposable asyncDisposable )
            {
                logger?.LogTrace( "disposing job {id} asynchronously", this.Id );
                await asyncDisposable.DisposeAsync();
            }

            // ReSharper disable once SuspiciousTypeConversion.Global
            if( job is IDisposable disposable )
            {
                logger?.LogTrace( "disposing job {id} synchronously", this.Id );
                disposable.Dispose();
            }
        }
    }
}
