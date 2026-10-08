using Microsoft.AI.Local.Adapters;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local;

/// <summary>A local model that generates text and chat responses. Its client is a standard <see cref="IChatClient"/>.</summary>
public interface ITextGenerationModel : ILocalModel<IChatClient>;

/// <summary>
/// Text-generation (chat) models from every provider, for example <c>LanguageModels.PhiSilica</c> (Windows) or
/// <c>LanguageModels.Phi4Mini</c> (Foundry Local).
/// </summary>
/// <remarks>
/// Getting a handle does no I/O. Each handle's documentation names the provider package it needs, for example
/// Microsoft.AI.Local.TextGeneration.Foundry. Without that package the handle reports
/// <see cref="ModelAvailabilityStatus.MissingAppRequirement"/> and analyzer MSAILOCAL201 warns at build time.
/// </remarks>
public static partial class LanguageModels
{
}

/// <summary>Extension methods for <see cref="ITextGenerationModel"/>.</summary>
public static class TextGenerationModelExtensions
{
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
}
