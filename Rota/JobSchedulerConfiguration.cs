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
        JobRunnerMaximumConcurrency = 100,

        // ... but restrict the jobs within each worker to one-at-a-time
        DefaultJobExecutionMode      = ExecutionMode.Consecutive,
        DefaultJobMaximumConcurrency = null,

        PollingRate = TimeSpan.FromMilliseconds( 500 ),
    };

    /// <summary>
    ///     The execution mode that all job runners in a scheduler will abide by.
    ///     See each member of the <see cref="ExecutionMode" /> <see langword="enum" /> for the specifics of each mode.
    /// </summary>
    public ExecutionMode JobRunnerExecutionMode { get; init; }

    /// <summary>
    ///     Optional. Only used when <see cref="JobRunnerExecutionMode" /> is <see cref="ExecutionMode.Concurrent" />.
    ///     If omitted or set to 0, concurrent executions will be unlimited.
    /// </summary>
    public ushort? JobRunnerMaximumConcurrency { get; init; }

    /// <summary>
    ///     The default execution mode of the jobs within each worker thread.
    ///     This need not be the same as the job runner's execution mode.
    ///     May be changed later by configuring a worker thread with <see cref="JobScheduler.ConfigureJobRunner{T}" />.
    ///     See each member of the <see cref="ExecutionMode" /> <see langword="enum" /> for the specifics of each mode.
    /// </summary>
    public ExecutionMode DefaultJobExecutionMode { get; init; }

    /// <summary>
    ///     Optional. Only used when <see cref="DefaultJobExecutionMode" /> is <see cref="ExecutionMode.Concurrent" />.
    ///     If omitted or set to 0, concurrent executions will be unlimited.
    /// </summary>
    public ushort? DefaultJobMaximumConcurrency { get; init; }

    /// <summary>
    ///     Used primarily in a hosted context by <seealso cref="Hosting.JobSchedulerService" /> to determine how often
    ///     <seealso cref="JobScheduler.RunJobsAsync" />
    ///     should be called.
    ///     This may also be used in a manual loop for the same purpose if desired.
    /// </summary>
    public TimeSpan PollingRate { get; init; }
}