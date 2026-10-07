using System.Diagnostics.CodeAnalysis;
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

/// <summary>An embedding generator that acquires its model and creates the real generator on first use.</summary>
internal sealed class LazyEmbeddingGenerator(ITextEmbeddingModel model) : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly LazyClient<IEmbeddingGenerator<string, Embedding<float>>> _lazy = new(model);

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var generator = await _lazy.GetAsync(cancellationToken).ConfigureAwait(false);
        return await generator.GenerateAsync(values, options, cancellationToken).ConfigureAwait(false);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        LazyServices.Resolve(this, _lazy.Model, _lazy.CreatedClient?.GetService(serviceType, serviceKey), serviceType, serviceKey,
            () => new EmbeddingGeneratorMetadata(model.ProviderName, null, model.Id));

    public void Dispose() => _lazy.Dispose();
}

/// <summary>A speech-to-text client that acquires its model and creates the real client on first use.</summary>
[Experimental(DiagnosticIds.Experiments.SpeechToText)]
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

internal static class LazyServices
{
    public static object? Resolve(object self, ILocalModel model, object? fromInner, Type serviceType, object? serviceKey, Func<object> metadataFactory)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (fromInner is not null)
        {
            return fromInner;
        }

        if (serviceKey is not null)
        {
            return null;
        }

        if (serviceType.IsInstanceOfType(self))
        {
            return self;
        }

        if (serviceType.IsInstanceOfType(model))
        {
            return model;
        }

        var metadata = metadataFactory();
        return serviceType.IsInstanceOfType(metadata) ? metadata : null;
    }
}
