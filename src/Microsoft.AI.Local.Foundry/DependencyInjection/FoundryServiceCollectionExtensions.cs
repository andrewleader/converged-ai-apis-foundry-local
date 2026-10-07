using Microsoft.AI.Local.Foundry;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the Foundry Local provider configuration with a service collection.</summary>
public static class FoundryServiceCollectionExtensions
{
    /// <summary>
    /// Configures the Foundry Local provider (app name, cache directory, logging, execution providers, unload policy).
    /// </summary>
    /// <remarks>
    /// <see cref="FoundryModels"/> handles are process-wide singletons, so this applies <see cref="FoundryProvider.Configure"/>
    /// immediately. Combine it with <c>AddLocalChatClient(FoundryModels.Phi4Mini)</c> and friends to register clients.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A callback that edits the provider options.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddFoundryLocal(this IServiceCollection services, Action<FoundryProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (configure is not null)
        {
            FoundryProvider.Configure(configure);
        }

        return services;
    }
}
