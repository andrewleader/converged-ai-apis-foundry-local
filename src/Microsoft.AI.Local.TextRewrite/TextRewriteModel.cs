using Microsoft.AI.Local.Adapters;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local;

/// <summary>A local model that rewrites text.</summary>
public interface ITextRewriteModel : ILocalModel<ITextRewriter>;

/// <summary>
/// Text-rewrite models from every provider, for example <c>TextRewriteModels.PhiSilica</c>. Any chat model can also
/// rewrite through <see cref="TextRewriteModelExtensions.AsTextRewriteModel"/>.
/// </summary>
/// <remarks>
/// Getting a handle does no I/O. Each handle's documentation names the provider package it needs. Without it the
/// handle reports <see cref="ModelAvailabilityStatus.MissingAppRequirement"/>.
/// </remarks>
public static partial class TextRewriteModels
{
}

/// <summary>Adapts chat models to text rewriting.</summary>
public static class TextRewriteModelExtensions
{
    /// <summary>Returns a rewrite model that prompts <paramref name="model"/>.</summary>
    /// <param name="model">A chat model (any <c>ITextGenerationModel</c>).</param>
    /// <returns>A rewrite model that shares <paramref name="model"/>'s acquisition and lifetime.</returns>
    public static ITextRewriteModel AsTextRewriteModel(this ILocalModel<IChatClient> model) => new ChatBackedRewriteModel(model);

    /// <summary>Returns an <see cref="ITextRewriter"/> that prompts <paramref name="chatClient"/>, which can be any chat client.</summary>
    /// <param name="chatClient">The chat client. It is not disposed with the rewriter.</param>
    /// <returns>A rewriter.</returns>
    public static ITextRewriter AsTextRewriter(this IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        var metadata = chatClient.GetService<ChatClientMetadata>();
        return new ChatClientTextRewriter(chatClient, metadata?.ProviderName, metadata?.DefaultModelId, ownsChatClient: false);
    }
}
