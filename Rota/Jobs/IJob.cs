namespace Rota.Jobs;

/// <summary>
///     An interface defining an executable job.
/// </summary>
public interface IJob
{
    /// <summary>
    ///     The optional name of the job. Used only to provide a friendly name to the thread.
    /// </summary>
    string? Name { get; }

    /// <summary>
    ///     Executes the job's business logic.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to signal cancellation of the job.</param>
    /// <returns>A task that completes when the job has finished execution.</returns>
    ValueTask ExecuteAsync( CancellationToken cancellationToken );
}