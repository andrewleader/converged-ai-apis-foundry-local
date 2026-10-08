using Microsoft.AI.Local.Adapters;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local;

/// <summary>A local model that summarizes text.</summary>
public interface ITextSummarizationModel : ILocalModel<ITextSummarizer>;

/// <summary>
/// Text-summarization models from every provider, for example <c>TextSummarizationModels.PhiSilica</c>. Any chat model
/// can also summarize through <see cref="TextSummarizationModelExtensions.AsTextSummarizationModel"/>.
/// </summary>
/// <remarks>
/// Getting a handle does no I/O. Each handle's documentation names the provider package it needs. Without it the
/// handle reports <see cref="ModelAvailabilityStatus.MissingAppRequirement"/>.
/// </remarks>
public static partial class TextSummarizationModels
{
}

/// <summary>Adapts chat models to text summarization.</summary>
public static class TextSummarizationModelExtensions
{
    /// <summary>
    /// Returns a summarization model that prompts <paramref name="model"/>, for example
    /// <c>LanguageModels.Phi4Mini.AsTextSummarizationModel()</c>. Use it to switch summarization between
    /// <c>TextSummarizationModels.PhiSilica</c> and any text-generation model.
    /// </summary>
    /// <param name="model">A chat model (any <c>ITextGenerationModel</c>).</param>
    /// <returns>A summarization model that shares <paramref name="model"/>'s acquisition and lifetime.</returns>
    public static ITextSummarizationModel AsTextSummarizationModel(this ILocalModel<IChatClient> model) => new ChatBackedSummarizationModel(model);

    /// <summary>Returns an <see cref="ITextSummarizer"/> that prompts <paramref name="chatClient"/>, which can be any chat client.</summary>
    /// <param name="chatClient">The chat client. It is not disposed with the summarizer.</param>
    /// <returns>A summarizer.</returns>
    public static ITextSummarizer AsTextSummarizer(this IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        var metadata = chatClient.GetService<ChatClientMetadata>();
        return new ChatClientTextSummarizer(chatClient, metadata?.ProviderName, metadata?.DefaultModelId, ownsChatClient: false);
    }
}
