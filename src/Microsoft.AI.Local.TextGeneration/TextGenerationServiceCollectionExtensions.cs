using Microsoft.AI.Local;
using Microsoft.Extensions.AI;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers lazily-acquired chat clients backed by local models.</summary>
public static class TextGenerationServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IChatClient"/> backed by <paramref name="model"/>. The model is acquired on the
    /// first request, so registration performs no I/O. Changing <paramref name="model"/> is the only edit needed to
    /// switch providers.
    /// </summary>
    /// <remarks>
    /// To add Microsoft.Extensions.AI middleware, use <c>services.AddChatClient(model.AsChatClient()).UseOpenTelemetry()</c> instead.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="model">The model, for example <c>LanguageModels.PhiSilica</c> or <c>LanguageModels.Phi4Mini</c>.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLocalChatClient(this IServiceCollection services, ITextGenerationModel model, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(model);
        return services.AddLocalClient<IChatClient, ITextGenerationModel>(model, null, _ => model.AsChatClient(), lifetime);
    }

    /// <summary>Registers a keyed <see cref="IChatClient"/> backed by <paramref name="model"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceKey">The service key.</param>
    /// <param name="model">The model.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKeyedLocalChatClient(this IServiceCollection services, object? serviceKey, ITextGenerationModel model, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(model);
        return services.AddLocalClient<IChatClient, ITextGenerationModel>(model, serviceKey, _ => model.AsChatClient(), lifetime);
    }
}
