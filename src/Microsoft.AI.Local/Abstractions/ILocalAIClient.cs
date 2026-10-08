namespace Microsoft.AI.Local;

/// <summary>
/// The common shape of every inference contract defined by this library (OCR, image description, text skills, ...).
/// </summary>
/// <remarks>
/// Follows the conventions of <c>Microsoft.Extensions.AI</c>: clients are disposable and expose
/// <see cref="GetService"/> as an escape hatch to the provider's native objects.
/// </remarks>
public interface ILocalAIClient : IDisposable
{
    /// <summary>Asks the client for an object of the specified type.</summary>
    /// <param name="serviceType">The type of object being requested.</param>
    /// <param name="serviceKey">An optional key that can be used to help identify the target service.</param>
    /// <returns>The found object, or <see langword="null"/>.</returns>
    /// <remarks>
    /// Use this to reach the provider's native object, for example
    /// <c>recognizer.GetService&lt;Microsoft.Windows.AI.Imaging.TextRecognizer&gt;()</c>, or the client's metadata.
    /// </remarks>
    object? GetService(Type serviceType, object? serviceKey = null);
}

/// <summary>
/// Extension methods for <see cref="ILocalAIClient"/>.
/// </summary>
public static class LocalAIClientExtensions
{
    /// <summary>Asks the client for an object of type <typeparamref name="TService"/>.</summary>
    /// <typeparam name="TService">The type of object being requested.</typeparam>
    /// <param name="client">The client.</param>
    /// <param name="serviceKey">An optional key that can be used to help identify the target service.</param>
    /// <returns>The found object, or <see langword="null"/>.</returns>
    public static TService? GetService<TService>(this ILocalAIClient client, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.GetService(typeof(TService), serviceKey) is TService service ? service : default;
    }

    /// <summary>Asks the client for an object of type <typeparamref name="TService"/> and throws if it isn't available.</summary>
    /// <typeparam name="TService">The type of object being requested.</typeparam>
    /// <param name="client">The client.</param>
    /// <param name="serviceKey">An optional key that can be used to help identify the target service.</param>
    /// <returns>The found object.</returns>
    /// <exception cref="InvalidOperationException">No such service is available.</exception>
    public static TService GetRequiredService<TService>(this ILocalAIClient client, object? serviceKey = null)
        where TService : notnull
    {
        return client.GetService<TService>(serviceKey)
            ?? throw new InvalidOperationException($"The client does not provide a service of type '{typeof(TService)}'.");
    }
}
