using System.Diagnostics.CodeAnalysis;
using Microsoft.AI.Local;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers local models and lazily-acquired clients with an <see cref="IServiceCollection"/>.
/// </summary>
public static class LocalAIServiceCollectionExtensions
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
    /// <param name="model">The model, for example <c>WindowsModels.PhiSilica</c> or <c>FoundryModels.Phi4Mini</c>.</param>
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

    /// <summary>Registers an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> backed by <paramref name="model"/>, acquired on first use.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="model">The model.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLocalEmbeddingGenerator(this IServiceCollection services, ITextEmbeddingModel model, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(model);
        return services.AddLocalClient<IEmbeddingGenerator<string, Embedding<float>>, ITextEmbeddingModel>(model, null, _ => model.AsEmbeddingGenerator(), lifetime);
    }

    /// <summary>Registers a keyed <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> backed by <paramref name="model"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceKey">The service key.</param>
    /// <param name="model">The model.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKeyedLocalEmbeddingGenerator(this IServiceCollection services, object? serviceKey, ITextEmbeddingModel model, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(model);
        return services.AddLocalClient<IEmbeddingGenerator<string, Embedding<float>>, ITextEmbeddingModel>(model, serviceKey, _ => model.AsEmbeddingGenerator(), lifetime);
    }

    /// <summary>Registers an <see cref="ISpeechToTextClient"/> backed by <paramref name="model"/>, acquired on first use.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="model">The model.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection.</returns>
    [Experimental("MSAILOCAL001")]
    public static IServiceCollection AddLocalSpeechToTextClient(this IServiceCollection services, ISpeechToTextModel model, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(model);
        return services.AddLocalClient<ISpeechToTextClient, ISpeechToTextModel>(model, null, _ => model.AsSpeechToTextClient(), lifetime);
    }

    /// <summary>
    /// Registers <paramref name="model"/> as <typeparamref name="TModel"/>, so services can take a model handle
    /// (for example to show acquisition progress) and create their own clients.
    /// </summary>
    /// <typeparam name="TModel">The model interface, for example <see cref="ITextRecognitionModel"/>.</typeparam>
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
    /// <typeparam name="TModel">The model interface, for example <see cref="ITextRecognitionModel"/>.</typeparam>
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

    private static IServiceCollection AddLocalClient<TClient, TModel>(
        this IServiceCollection services,
        TModel model,
        object? serviceKey,
        Func<IServiceProvider, TClient> factory,
        ServiceLifetime lifetime)
        where TClient : class
        where TModel : class, ILocalModel
    {
        ArgumentNullException.ThrowIfNull(services);

        // Factory registrations let the container own (and dispose) the client.
        if (serviceKey is null)
        {
            services.Add(new ServiceDescriptor(typeof(TClient), factory, lifetime));
            services.TryAdd(ServiceDescriptor.Singleton(model));
        }
        else
        {
            services.Add(new ServiceDescriptor(typeof(TClient), serviceKey, (sp, _) => factory(sp), lifetime));
            services.TryAdd(ServiceDescriptor.KeyedSingleton(serviceKey, model));
        }

        return services;
    }
}
