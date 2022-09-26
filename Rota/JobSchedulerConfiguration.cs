namespace Rota;

/// <summary>
///     Defines the behaviour of a <see cref="JobScheduler" />.
/// </summary>
public sealed class JobSchedulerConfiguration
{
    /// <summary>
    ///     The default job scheduler configuration options.
    /// </summary>
    public static JobSchedulerConfiguration Default { get; } = new()
    {
        // allow (theoretically) all workers to spin up at the same time ...
        JobRunnerExecutionMode      = ExecutionMode.Concurrent,
        JobRunnerMaximumConcurrency = null,

        // ... but restrict the jobs within each worker to one-at-a-time
        JobSchedulerExecutionMode      = ExecutionMode.Consecutive,
        JobSchedulerMaximumConcurrency = null,
        PollingRate                    = TimeSpan.FromMilliseconds( 500 ),
        ErrorHandlingStrategy          = ErrorHandlingStrategy.StopScheduler,
        ErrorHandler                   = null,
    };

    /// <summary>
    ///     The execution mode of the job runner, determining how the jobs within the runner thread will be spun up.
    ///     This need not be the same as the scheduler's execution mode.
    ///     See each member of the <see cref="ExecutionMode" /> <see langword="enum" /> for the specifics of each mode.
    /// </summary>
    public ExecutionMode JobRunnerExecutionMode { get; init; }

    /// <summary>
    ///     Optional. Only used when <see cref="JobRunnerExecutionMode" /> is <see cref="ExecutionMode.Concurrent" />.
    ///     If omitted this will default to twice the number of available logical CPU cores
    ///     (i.e. 8 for a quad-core without SMT, or 16 for a quad-core with SMT). If set to 0, it will allow for unlimited
    ///     concurrency.
    /// </summary>
    public ushort? JobRunnerMaximumConcurrency { get; init; }

    /// <summary>
    ///     The execution mode of the job scheduler, determining how the job runner threads will be spun up by the scheduler.
    ///     This need not be the same as the job runner's execution mode.
    ///     See each member of the <see cref="ExecutionMode" /> <see langword="enum" /> for the specifics of each mode.
    /// </summary>
    public ExecutionMode JobSchedulerExecutionMode { get; init; }

    /// <summary>
    ///     Optional. Only used when <see cref="JobSchedulerExecutionMode" /> is <see cref="ExecutionMode.Concurrent" />.
    ///     If omitted this will default to the number of available logical CPU cores
    ///     (i.e. 4 for a quad-core without SMT, or 8 for a quad-core with SMT). If set to 0, it will allow for unlimited
    ///     concurrency.
    /// </summary>
    public ushort? JobSchedulerMaximumConcurrency { get; init; }

    /// <summary>
    ///     Used primarily in a hosted context by <seealso cref="Hosting.JobSchedulerService" /> to determine how often
    ///     <seealso cref="JobScheduler.RunJobsAsync" />
    ///     should be called.
    ///     This may also be used in a manual loop for the same purpose if desired.
    /// </summary>
    public TimeSpan PollingRate { get; init; }

    /// <summary>
    ///     Used to determine how the job scheduler should behave when encountering exceptions.
    /// </summary>
    public ErrorHandlingStrategy ErrorHandlingStrategy { get; init; }

    /// <summary>
    ///     A callback that will be invoked whenever the scheduler encounters an exception,
    ///     regardless of what <see cref="ErrorHandlingStrategy" /> is set to.
    /// </summary>
    public Action<Exception>? ErrorHandler { get; init; }
}
