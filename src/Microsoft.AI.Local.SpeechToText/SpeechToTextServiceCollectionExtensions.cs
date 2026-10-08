using System.Diagnostics.CodeAnalysis;
using Microsoft.AI.Local;
using Microsoft.Extensions.AI;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers lazily-acquired speech-to-text clients backed by local models.</summary>
[Experimental("MSAILOCAL001")]
public static class SpeechToTextServiceCollectionExtensions
{
    /// <summary>Registers an <see cref="ISpeechToTextClient"/> backed by <paramref name="model"/>, acquired on first use.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="model">The model.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLocalSpeechToTextClient(this IServiceCollection services, ISpeechToTextModel model, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(model);
        return services.AddLocalClient<ISpeechToTextClient, ISpeechToTextModel>(model, null, _ => model.AsSpeechToTextClient(), lifetime);
    }

    /// <summary>Registers a keyed <see cref="ISpeechToTextClient"/> backed by <paramref name="model"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceKey">The service key.</param>
    /// <param name="model">The model.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKeyedLocalSpeechToTextClient(this IServiceCollection services, object? serviceKey, ISpeechToTextModel model, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(model);
        return services.AddLocalClient<ISpeechToTextClient, ISpeechToTextModel>(model, serviceKey, _ => model.AsSpeechToTextClient(), lifetime);
    }
}
