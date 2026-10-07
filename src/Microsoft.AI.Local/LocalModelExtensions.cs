using System.Diagnostics.CodeAnalysis;
using Microsoft.AI.Local.Adapters;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local;

/// <summary>
/// Extension methods that adapt local models to other task types and to lazily-acquired clients.
/// </summary>
public static class LocalModelExtensions
{
    /// <summary>
    /// Returns a summarization model that prompts <paramref name="model"/>. Use it to switch summarization between
    /// the inbox <c>WindowsModels.TextSummarization</c> and any text-generation model.
    /// </summary>
    /// <param name="model">The text-generation model.</param>
    /// <returns>A summarization model that shares <paramref name="model"/>'s acquisition and lifetime.</returns>
    public static ITextSummarizationModel AsTextSummarizationModel(this ITextGenerationModel model) => new ChatBackedSummarizationModel(model);

    /// <summary>Returns a rewrite model that prompts <paramref name="model"/>.</summary>
    /// <param name="model">The text-generation model.</param>
    /// <returns>A rewrite model that shares <paramref name="model"/>'s acquisition and lifetime.</returns>
    public static ITextRewriteModel AsTextRewriteModel(this ITextGenerationModel model) => new ChatBackedRewriteModel(model);

    /// <summary>Returns a text-to-table model that prompts <paramref name="model"/> for JSON.</summary>
    /// <param name="model">The text-generation model.</param>
    /// <returns>A text-to-table model that shares <paramref name="model"/>'s acquisition and lifetime.</returns>
    public static ITextToTableModel AsTextToTableModel(this ITextGenerationModel model) => new ChatBackedTextToTableModel(model);

    /// <summary>
    /// Returns an image-description model that prompts <paramref name="model"/> with the image.
    /// The model must accept image input (<see cref="LocalModelCapabilities.SupportsImageInput"/>).
    /// </summary>
    /// <param name="model">A vision-capable text-generation model.</param>
    /// <returns>An image-description model that shares <paramref name="model"/>'s acquisition and lifetime.</returns>
    /// <exception cref="ArgumentException"><paramref name="model"/> doesn't accept image input.</exception>
    public static IImageDescriptionModel AsImageDescriptionModel(this ITextGenerationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!model.Capabilities.SupportsImageInput)
        {
            throw new ArgumentException($"The model '{model.Id}' does not accept image input.", nameof(model));
        }

        return new ChatBackedImageDescriptionModel(model);
    }

    /// <summary>
    /// Returns an <see cref="IChatClient"/> that acquires <paramref name="model"/> on first use. Use it with
    /// Microsoft.Extensions.AI's <c>ChatClientBuilder</c>, for example
    /// <c>services.AddChatClient(model.AsChatClient()).UseOpenTelemetry()</c>.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>A lazily-initialized chat client. Disposing it disposes the underlying client.</returns>
    public static IChatClient AsChatClient(this ITextGenerationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new LazyChatClient(model);
    }

    /// <summary>Returns an embedding generator that acquires <paramref name="model"/> on first use.</summary>
    /// <param name="model">The model.</param>
    /// <returns>A lazily-initialized embedding generator. Disposing it disposes the underlying generator.</returns>
    public static IEmbeddingGenerator<string, Embedding<float>> AsEmbeddingGenerator(this ITextEmbeddingModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new LazyEmbeddingGenerator(model);
    }

    /// <summary>Returns a speech-to-text client that acquires <paramref name="model"/> on first use.</summary>
    /// <param name="model">The model.</param>
    /// <returns>A lazily-initialized speech-to-text client. Disposing it disposes the underlying client.</returns>
    [Experimental(DiagnosticIds.Experiments.SpeechToText)]
    public static ISpeechToTextClient AsSpeechToTextClient(this ISpeechToTextModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new LazySpeechToTextClient(model);
    }

    /// <summary>Returns an <see cref="ITextSummarizer"/> that prompts <paramref name="chatClient"/>, which can be any chat client.</summary>
    /// <param name="chatClient">The chat client. It is not disposed with the summarizer.</param>
    /// <returns>A summarizer.</returns>
    public static ITextSummarizer AsTextSummarizer(this IChatClient chatClient) =>
        new ChatClientTextSummarizer(chatClient, GetProviderName(chatClient), GetModelId(chatClient), ownsChatClient: false);

    /// <summary>Returns an <see cref="ITextRewriter"/> that prompts <paramref name="chatClient"/>, which can be any chat client.</summary>
    /// <param name="chatClient">The chat client. It is not disposed with the rewriter.</param>
    /// <returns>A rewriter.</returns>
    public static ITextRewriter AsTextRewriter(this IChatClient chatClient) =>
        new ChatClientTextRewriter(chatClient, GetProviderName(chatClient), GetModelId(chatClient), ownsChatClient: false);

    /// <summary>Returns an <see cref="ITextToTableConverter"/> that prompts <paramref name="chatClient"/>, which can be any chat client.</summary>
    /// <param name="chatClient">The chat client. It is not disposed with the converter.</param>
    /// <returns>A text-to-table converter.</returns>
    public static ITextToTableConverter AsTextToTableConverter(this IChatClient chatClient) =>
        new ChatClientTextToTableConverter(chatClient, GetProviderName(chatClient), GetModelId(chatClient), ownsChatClient: false);

    /// <summary>Returns an <see cref="IImageDescriber"/> that prompts <paramref name="chatClient"/>, which must accept image input.</summary>
    /// <param name="chatClient">The chat client. It is not disposed with the describer.</param>
    /// <returns>An image describer.</returns>
    public static IImageDescriber AsImageDescriber(this IChatClient chatClient) =>
        new ChatClientImageDescriber(chatClient, GetProviderName(chatClient), GetModelId(chatClient), ownsChatClient: false);

    private static string? GetProviderName(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        return chatClient.GetService<ChatClientMetadata>()?.ProviderName;
    }

    private static string? GetModelId(IChatClient chatClient) => chatClient.GetService<ChatClientMetadata>()?.DefaultModelId;
}
