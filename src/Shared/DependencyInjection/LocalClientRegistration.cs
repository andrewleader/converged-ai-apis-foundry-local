using Microsoft.AI.Local;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>The registration logic shared by the task packages' <c>AddLocal*Client</c> methods.</summary>
internal static class LocalClientRegistration
{
    public static IServiceCollection AddLocalClient<TClient, TModel>(
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
