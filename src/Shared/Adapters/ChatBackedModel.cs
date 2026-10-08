using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Adapters;

/// <summary>
/// A task model implemented by prompting a chat model. Acquisition is delegated to the underlying model, so the
/// adapter shares its download, load and lifetime.
/// </summary>
internal abstract class ChatBackedModel<TClient> : ILocalModel<TClient>
    where TClient : class, ILocalAIClient
{
    protected ChatBackedModel(ILocalModel<IChatClient> inner)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public ILocalModel<IChatClient> Inner { get; }

    public string Id => Inner.Id;

    public string DisplayName => Inner.DisplayName;

    public string ProviderName => Inner.ProviderName;

    public LocalModelCapabilities Capabilities => Inner.Capabilities;

    public ValueTask<ModelAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default) =>
        Inner.GetAvailabilityAsync(cancellationToken);

    public Task<ModelAvailability> EnsureReadyAsync(IProgress<ModelAcquisitionProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Inner.EnsureReadyAsync(progress, cancellationToken);

    public async Task<TClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        var chat = await Inner.CreateClientAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Create(chat);
        }
        catch
        {
            chat.Dispose();
            throw;
        }
    }

    public override string ToString() => Id;

    protected abstract TClient Create(IChatClient chatClient);
}
