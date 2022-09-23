namespace Rota;

/// <summary>
///     Determines how the scheduler and worker threads should deal with exceptions raised within the executing job(s).
/// </summary>
public enum ErrorHandlingStrategy : byte
{
    /// <summary>
    ///     Ignore the exception and keep running as normal.
    /// </summary>
    Ignore,

    /// <summary>
    ///     Disables the entire job scheduler when any job or job runner throws an exception unless and until manually
    ///     re-enabled.
    /// </summary>
    StopScheduler,

    /// <summary>
    ///     Disables the individual job that threw the exception unless and until manually re-enabled.
    /// </summary>
    DisableJob,

    /// <summary>
    ///     Disables the individual job runner that threw the exception unless and until manually re-enabled.
    /// </summary>
    DisableWorker,
}
