using Microsoft.AI.Local.Adapters;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local;

/// <summary>A local model that converts text to a table.</summary>
public interface ITextToTableModel : ILocalModel<ITextToTableConverter>;

/// <summary>
/// Text-to-table models from every provider, for example <c>TextToTableModels.PhiSilica</c>. Any chat model can also
/// convert text to tables through <see cref="TextToTableModelExtensions.AsTextToTableModel"/>.
/// </summary>
/// <remarks>
/// Getting a handle does no I/O. Each handle's documentation names the provider package it needs. Without it the
/// handle reports <see cref="ModelAvailabilityStatus.MissingAppRequirement"/>.
/// </remarks>
public static partial class TextToTableModels
{
}

/// <summary>Adapts chat models to text-to-table conversion.</summary>
public static class TextToTableModelExtensions
{
    /// <summary>Returns a text-to-table model that prompts <paramref name="model"/> for JSON.</summary>
    /// <param name="model">A chat model (any <c>ITextGenerationModel</c>).</param>
    /// <returns>A text-to-table model that shares <paramref name="model"/>'s acquisition and lifetime.</returns>
    public static ITextToTableModel AsTextToTableModel(this ILocalModel<IChatClient> model) => new ChatBackedTextToTableModel(model);

    /// <summary>Returns an <see cref="ITextToTableConverter"/> that prompts <paramref name="chatClient"/>, which can be any chat client.</summary>
    /// <param name="chatClient">The chat client. It is not disposed with the converter.</param>
    /// <returns>A text-to-table converter.</returns>
    public static ITextToTableConverter AsTextToTableConverter(this IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        var metadata = chatClient.GetService<ChatClientMetadata>();
        return new ChatClientTextToTableConverter(chatClient, metadata?.ProviderName, metadata?.DefaultModelId, ownsChatClient: false);
    }
}
