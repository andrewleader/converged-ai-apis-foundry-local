using System.Diagnostics.CodeAnalysis;
using Microsoft.AI.Local.Adapters;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local;

/// <summary>A local model that transcribes speech. Its client is a standard <see cref="ISpeechToTextClient"/>.</summary>
[Experimental(SpeechToTextDiagnostics.ExperimentalId)]
public interface ISpeechToTextModel : ILocalModel<ISpeechToTextClient>;

/// <summary>Speech-to-text models from every provider, for example <c>SpeechToTextModels.WhisperTiny</c>.</summary>
/// <remarks>
/// Getting a handle does no I/O. Each handle's documentation names the provider package it needs. Without it the
/// handle reports <see cref="ModelAvailabilityStatus.MissingAppRequirement"/>.
/// </remarks>
[Experimental(SpeechToTextDiagnostics.ExperimentalId)]
public static partial class SpeechToTextModels
{
}

/// <summary>Extension methods for <see cref="ISpeechToTextModel"/>.</summary>
[Experimental(SpeechToTextDiagnostics.ExperimentalId)]
public static class SpeechToTextModelExtensions
{
    /// <summary>Returns a speech-to-text client that acquires <paramref name="model"/> on first use.</summary>
    /// <param name="model">The model.</param>
    /// <returns>A lazily-initialized speech-to-text client. Disposing it disposes the underlying client.</returns>
    public static ISpeechToTextClient AsSpeechToTextClient(this ISpeechToTextModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new LazySpeechToTextClient(model);
    }
}

/// <summary>Diagnostic identifiers of the speech-to-text package.</summary>
internal static class SpeechToTextDiagnostics
{
    /// <summary>Speech-to-text builds on the experimental <c>ISpeechToTextClient</c> from Microsoft.Extensions.AI.</summary>
    public const string ExperimentalId = "MSAILOCAL001";
}
