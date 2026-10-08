using Microsoft.AI.Local;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers local model handles with an <see cref="IServiceCollection"/>. Each task package adds the registration of
/// its client (for example <c>AddLocalChatClient</c> in Microsoft.AI.Local.TextGeneration).
/// </summary>
public static class LocalAIServiceCollectionExtensions
{
    /// <summary>
    /// Registers <paramref name="model"/> as <typeparamref name="TModel"/>, so services can take a model handle
    /// (for example to show acquisition progress) and create their own clients.
    /// </summary>
    /// <typeparam name="TModel">The model interface, for example <c>ITextRecognitionModel</c>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="model">The model.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLocalModel<TModel>(this IServiceCollection services, TModel model)
        where TModel : class, ILocalModel
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(model);
        services.Add(ServiceDescriptor.Singleton(model));
        return services;
    }

    /// <summary>Registers <paramref name="model"/> as a keyed <typeparamref name="TModel"/>.</summary>
    /// <typeparam name="TModel">The model interface, for example <c>ITextRecognitionModel</c>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceKey">The service key.</param>
    /// <param name="model">The model.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKeyedLocalModel<TModel>(this IServiceCollection services, object? serviceKey, TModel model)
        where TModel : class, ILocalModel
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(model);
        services.Add(ServiceDescriptor.KeyedSingleton(serviceKey, model));
        return services;
    }
}
