using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rota.Scheduling;

namespace Rota.Jobs;

internal readonly record struct ScheduledJob(
    Guid                    Id,
    Type                    JobType,
    Schedule                Schedule,
    ImmutableArray<object?> ConstructorArguments
)
{
    public async ValueTask ExecuteAsync( IServiceProvider? provider, CancellationToken cancellationToken )
    {
        if( cancellationToken.IsCancellationRequested || !this.Schedule.IsDue( DateTime.UtcNow ) ) return;

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

        logger?.LogTrace( "executing job {id}", this.Id );
        var job = (IJob)jobInstance;

        if( !String.IsNullOrWhiteSpace( job.Name ) ) Thread.CurrentThread.Name += $" [{job.Name}]";

        await job.ExecuteAsync( cancellationToken );

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
