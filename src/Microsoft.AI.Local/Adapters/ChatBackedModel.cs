using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Adapters;

/// <summary>
/// A task model implemented by prompting an <see cref="ITextGenerationModel"/>. Acquisition is delegated to the
/// underlying model, so the adapter shares its download, load and lifetime.
/// </summary>
internal abstract class ChatBackedModel<TClient> : ILocalModel<TClient>
    where TClient : class, ILocalAIClient
{
    protected ChatBackedModel(ITextGenerationModel inner)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public ITextGenerationModel Inner { get; }

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

internal sealed class ChatBackedSummarizationModel(ITextGenerationModel inner) : ChatBackedModel<ITextSummarizer>(inner), ITextSummarizationModel
{
    protected override ITextSummarizer Create(IChatClient chatClient) => new ChatClientTextSummarizer(chatClient, ProviderName, Id, ownsChatClient: true);
}

internal sealed class ChatBackedRewriteModel(ITextGenerationModel inner) : ChatBackedModel<ITextRewriter>(inner), ITextRewriteModel
{
    protected override ITextRewriter Create(IChatClient chatClient) => new ChatClientTextRewriter(chatClient, ProviderName, Id, ownsChatClient: true);
}

internal sealed class ChatBackedTextToTableModel(ITextGenerationModel inner) : ChatBackedModel<ITextToTableConverter>(inner), ITextToTableModel
{
    protected override ITextToTableConverter Create(IChatClient chatClient) => new ChatClientTextToTableConverter(chatClient, ProviderName, Id, ownsChatClient: true);
}

internal sealed class ChatBackedImageDescriptionModel(ITextGenerationModel inner) : ChatBackedModel<IImageDescriber>(inner), IImageDescriptionModel
{
    protected override IImageDescriber Create(IChatClient chatClient) => new ChatClientImageDescriber(chatClient, ProviderName, Id, ownsChatClient: true);
}
