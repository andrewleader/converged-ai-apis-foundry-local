using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Adapters;

/// <summary>A speech-to-text client that acquires its model and creates the real client on first use.</summary>
[Experimental(SpeechToTextDiagnostics.ExperimentalId)]
internal sealed class LazySpeechToTextClient(ISpeechToTextModel model) : ISpeechToTextClient
{
    private readonly LazyClient<ISpeechToTextClient> _lazy = new(model);

    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream, SpeechToTextOptions? options = null, CancellationToken cancellationToken = default)
    {
        var client = await _lazy.GetAsync(cancellationToken).ConfigureAwait(false);
        return await client.GetTextAsync(audioSpeechStream, options, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await _lazy.GetAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var update in client.GetStreamingTextAsync(audioSpeechStream, options, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        LazyServices.Resolve(this, _lazy.Model, _lazy.CreatedClient?.GetService(serviceType, serviceKey), serviceType, serviceKey,
            () => new SpeechToTextClientMetadata(model.ProviderName, null, model.Id));

    public void Dispose() => _lazy.Dispose();
}
