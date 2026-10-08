using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Adapters;

/// <summary>An <see cref="IImageDescriber"/> implemented by prompting a vision-capable chat model.</summary>
internal sealed class ChatClientImageDescriber(IChatClient chatClient, string? providerName, string? modelId, bool ownsChatClient)
    : ChatClientSkillBase(chatClient, providerName, modelId, ownsChatClient), IImageDescriber
{
    public ImageDescriberMetadata Metadata { get; } = new(providerName, modelId);

    protected override object MetadataObject => Metadata;

    public async Task<ImageDescriptionResult> DescribeAsync(ImageFrame image, ImageDescriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        var request = (options?.Kind ?? ImageDescriptionKind.Brief) switch
        {
            ImageDescriptionKind.Detailed => "Describe this image in detail, including the main subjects, setting, colors and any text.",
            ImageDescriptionKind.Diagram => "This image is a chart or diagram. Describe its type, what it shows, and its key data points and relationships.",
            ImageDescriptionKind.Accessible => "Write alternative text for this image for a person who can't see it. Be concise and objective, and transcribe any important text.",
            _ => "Describe this image in one short sentence.",
        };

        var user = new ChatMessage(ChatRole.User, [new TextContent(request), image.ToDataContent()]);
        var response = await CompleteAsync(
            "You describe images accurately. Respond with the description only, without a preamble.",
            user,
            CreateChatOptions(options),
            cancellationToken).ConfigureAwait(false);
        return Complete(new ImageDescriptionResult(response.Text.Trim()), response);
    }
}

internal sealed class ChatBackedImageDescriptionModel(ILocalModel<IChatClient> inner) : ChatBackedModel<IImageDescriber>(inner), IImageDescriptionModel
{
    protected override IImageDescriber Create(IChatClient chatClient) => new ChatClientImageDescriber(chatClient, ProviderName, Id, ownsChatClient: true);
}
