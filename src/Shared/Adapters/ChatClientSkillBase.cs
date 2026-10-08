using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Adapters;

/// <summary>Shared plumbing for the prompt-based skills implemented over an <see cref="IChatClient"/>.</summary>
internal abstract class ChatClientSkillBase : ILocalAIClient
{
    private readonly bool _ownsChatClient;

    protected ChatClientSkillBase(IChatClient chatClient, string? providerName, string? modelId, bool ownsChatClient)
    {
        ChatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        ProviderName = providerName;
        ModelId = modelId;
        _ownsChatClient = ownsChatClient;
    }

    protected IChatClient ChatClient { get; }

    protected string? ProviderName { get; }

    protected string? ModelId { get; }

    protected abstract object MetadataObject { get; }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is null)
        {
            if (serviceType.IsInstanceOfType(this))
            {
                return this;
            }

            if (serviceType.IsInstanceOfType(MetadataObject))
            {
                return MetadataObject;
            }
        }

        return ChatClient.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        if (_ownsChatClient)
        {
            ChatClient.Dispose();
        }
    }

    protected async Task<ChatResponse> CompleteAsync(
        string instructions,
        ChatMessage user,
        ChatOptions? chatOptions,
        CancellationToken cancellationToken)
    {
        ChatMessage[] messages = [new(ChatRole.System, instructions), user];
        var response = await ChatClient.GetResponseAsync(messages, chatOptions, cancellationToken).ConfigureAwait(false);
        if (response.FinishReason == ChatFinishReason.ContentFilter)
        {
            throw new LocalModelContentFilteredException("The response was blocked by content moderation.") { ModelId = ModelId };
        }

        return response;
    }

    protected TResult Complete<TResult>(TResult result, ChatResponse response)
        where TResult : LocalAIResult
    {
        result.ModelId = response.ModelId ?? ModelId;
        result.RawRepresentation = response;
        return result;
    }

    protected static ChatOptions CreateChatOptions(LocalAIRequestOptions? options) => new()
    {
        Temperature = 0.2f,
        AdditionalProperties = options?.AdditionalProperties?.Clone(),
    };

    protected static string Delimit(string text) => $"<text>\n{text}\n</text>";
}
