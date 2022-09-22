namespace Rota;

/// <summary>
///     Defines the execution modes for worker threads and the jobs within those threads.
///     Worker threads may be executed by the scheduler in a different mode than the jobs within each worker.
/// </summary>
public enum ExecutionMode : byte
{
    /// <summary>
    ///     Denotes that the workers or jobs should execute simultaneously.
    /// </summary>
    Concurrent,

    /// <summary>
    ///     Denotes that each worker or job thread should only begin execution once the previous thread has exited.
    /// </summary>
    Consecutive,
}
