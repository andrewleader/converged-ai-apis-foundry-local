using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Adapters;

/// <summary>An <see cref="IChatClient"/> that acquires its model and creates the real client on first use.</summary>
internal sealed class LazyChatClient(ITextGenerationModel model) : IChatClient
{
    private readonly LazyClient<IChatClient> _lazy = new(model);

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var client = await _lazy.GetAsync(cancellationToken).ConfigureAwait(false);
        return await client.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await _lazy.GetAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var update in client.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        LazyServices.Resolve(this, _lazy.Model, _lazy.CreatedClient?.GetService(serviceType, serviceKey), serviceType, serviceKey,
            () => new ChatClientMetadata(model.ProviderName, null, model.Id));

    public void Dispose() => _lazy.Dispose();
}
