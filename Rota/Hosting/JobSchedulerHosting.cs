using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Rota.Hosting;

/// <summary>
///     A set of extension methods to help with registering a <see cref="JobScheduler" /> to a hosting context.
/// </summary>
public static class JobSchedulerHosting
{
    /// <summary>
    ///     Configures an instance of <see cref="JobSchedulerConfiguration" /> and registers it with the dependency injection
    ///     container.
    /// </summary>
    /// <param name="builder">The instance of the host builder currently being configured.</param>
    /// <param name="configurator">
    ///     A factory <see langword="delegate" /> to construct a new instance of
    ///     <see cref="JobSchedulerConfiguration" />.
    /// </param>
    /// <returns>The instance of the host builder currently being configured.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="builder" /> or <paramref name="configurator" /> is
    ///     <see langword="null" />.
    /// </exception>
    public static IHostBuilder ConfigureScheduler(
        this IHostBuilder                                 builder,
        Func<IServiceProvider, JobSchedulerConfiguration> configurator
    )
    {
        if( builder is null ) throw new ArgumentNullException( nameof( builder ) );
        if( configurator is null ) throw new ArgumentNullException( nameof( configurator ) );

        return builder.ConfigureServices( ( _, services ) => services.AddSingleton( configurator ) );
    }

    /// <summary>
    ///     Configures an instance of <see cref="JobSchedulerConfiguration" /> and registers it with the dependency injection
    ///     container.
    /// </summary>
    /// <param name="builder">The instance of the host builder currently being configured.</param>
    /// <param name="configurator">
    ///     A factory <see langword="delegate" /> to construct a new instance of
    ///     <see cref="JobSchedulerConfiguration" />.
    /// </param>
    /// <returns>The instance of the host builder currently being configured.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="builder" /> or <paramref name="configurator" /> is
    ///     <see langword="null" />.
    /// </exception>
    public static IHostBuilder ConfigureScheduler(
        this IHostBuilder                                                     builder,
        Func<HostBuilderContext, IServiceProvider, JobSchedulerConfiguration> configurator
    )
    {
        if( builder is null ) throw new ArgumentNullException( nameof( builder ) );
        if( configurator is null ) throw new ArgumentNullException( nameof( configurator ) );

        return builder.ConfigureServices(
            ( host, collection ) => collection.AddSingleton( provider => configurator( host, provider ) )
        );
    }

    /// <summary>
    ///     Adds a new instance of <see cref="JobScheduler" /> to the host.
    ///     This will use the configuration setup with
    ///     <see
    ///         cref="ConfigureScheduler(Microsoft.Extensions.Hosting.IHostBuilder,System.Func{System.IServiceProvider,Rota.JobSchedulerConfiguration})" />
    ///     if present, otherwise a default configuration object will be used.
    ///     but if not
    /// </summary>
    /// <param name="builder">The instance of the host builder currently being configured.</param>
    /// <returns>The instance of the host builder currently being configured.</returns>
    public static IHostBuilder AddScheduler( this IHostBuilder builder )
    {
        return builder.ConfigureServices(
            ( _, services ) => services.AddSingleton(
                                            provider => new JobScheduler(
                                                provider.GetService<JobSchedulerConfiguration>(),
                                                provider
                                            )
                                        )
                                       .AddHostedService<JobSchedulerService>()
        );
    }

    /// <summary>
    ///     Retrieves the <see cref="JobScheduler" /> instance from the service container and makes it available for use.
    /// </summary>
    /// <param name="host">The instance of the host that has a scheduler registered.</param>
    /// <param name="action">A <see langword="delegate" /> that operates on the registered scheduler.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="host" /> or <paramref name="action" /> is
    ///     <see langword="null" />.
    /// </exception>
    public static void UseScheduler( this IHost host, Action<JobScheduler> action )
    {
        if( action is null ) throw new ArgumentNullException( nameof( action ) );
        action( host.GetScheduler() );
    }

    /// <summary>
    ///     Retrieves the <see cref="JobScheduler" /> instance from the service container and makes it available for use.
    /// </summary>
    /// <param name="host">The instance of the host that has a scheduler registered.</param>
    /// <returns>The registered <see cref="JobScheduler" />.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="host" /> is <see langword="null" />.
    /// </exception>
    public static JobScheduler GetScheduler( this IHost host )
    {
        if( host is null ) throw new ArgumentNullException( nameof( host ) );
        return host.Services.GetRequiredService<JobScheduler>();
    }
}
