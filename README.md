# Rota

A simple yet robust job scheduling library that aims to be simpler and easier to use
than [Quartz.NET](https://www.quartz-scheduler.net/), but also more robust and configurable than other simple schedulers
like [Coravel](https://github.com/jamesmh/coravel) while providing sensible default configuration options such that for
light use little to no configuration is actually required beyond setting up job schedules.

Rota was heavily inspired by Coravel.

# Features

- [x] Job scheduling
    - [x] Simple schedule that triggers on an interval.
    - [x] Schedule that uses cron expressions.
    - [x] Schedule that uses a rate limiter to further restrict execution.
    - [ ] Complex schedule that can build highly specific schedules without needing to know cron expressions.
- [x] Multiple execution modes.
    - [x] Concurrent/parallel execution.
    - [x] Consecutive execution.
- [x] Granular control of configuration.
- [x] [Microsoft.Extensions.Hosting](https://docs.microsoft.com/en-us/dotnet/core/extensions/generic-host) support.
- [ ] Caching & persistence for schedules, and jobs.
    - [ ] Built-in cache providers.
        - [ ] JSON
        - [ ] XML
        - [ ] MongoDB
        - [ ] PgSQL
        - [ ] MySQL/MariaDB
        - [ ] SQLite
        - [ ] [SurrealDB](https://github.com/surrealdb/surrealdb)

# Quick Start

Rota is available on [NuGet](https://www.nuget.org/packages/Rota/).

## Manual

Below is a minimal example of a job that prints `Hello, World!` to the console every 10 seconds on a manually managed
job scheduler.

``` csharp
using Rota;
using Rota.Jobs;
using Rota.Scheduling;

var scheduler = new JobScheduler();
var everyTenSeconds = Schedule.FromInterval( TimeSpan.FromSeconds( 10 ) ).RunOnceAtStartup();

scheduler.ScheduleJob<HelloWorldJob>( everyTenSeconds ); // HelloWorldJob will execute every 10 seconds

while( !scheduler.IsCancellationRequested )
{
    await scheduler.RunJobsAsync(); // run all jobs that are due to be executed
    await Task.Delay(
        scheduler.Configuration.PollingRate // the interval at which RunJobsAsync() should be called.
    );
}

public sealed class HelloWorldJob : IJob
{
    public async ValueTask ExecuteAsync( CancellationToken cancellationToken )
        => await Console.Out.WriteLineAsync( "Hello, World!" );
}

```

## Hosted

Below is a minimal example of a job that prints `Hello, World!` to the console every 10 seconds using a scheduler
registered to a hosting context. It also demonstrates dependency injection support by using an `ILogger` instance to
print to the console.

``` csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rota.Hosting;
using Rota.Jobs;
using Rota.Scheduling;

var host = Host.CreateDefaultBuilder()
               .ConfigureDefaults( args )
               .UseConsoleLifetime()
               .AddScheduler()
               .Build();

host.UseScheduler(
    scheduler => {
        var everyTenSeconds = Schedule.FromInterval( TimeSpan.FromSeconds( 10 ) ).RunOnceAtStartup();

        scheduler.ScheduleJob<HelloWorldJob>( everyTenSeconds ); // HelloWorldJob will execute every 10 seconds
    }
);

await host.RunAsync();

public sealed class HelloWorldJob : IJob
{
    private readonly ILogger<HelloWorldJob> _logger;

    public HelloWorldJob( ILogger<HelloWorldJob> logger ) => this._logger = logger;

    public ValueTask ExecuteAsync( CancellationToken cancellationToken )
    {
        this._logger.LogInformation( "Hello, World!" );
        return ValueTask.CompletedTask;
    }
}

```
