using Microsoft.Extensions.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Text;

namespace Microsoft.AI.Local.Windows.Providers;

/// <summary>Conversions between this library's types and the Windows AI API types, for Windows task provider packages.</summary>
public static class WindowsAIInterop
{
    /// <summary>Gets the content-filter thresholds set with <c>WithWindowsContentFilter</c>, if any.</summary>
    /// <param name="properties">The request's additional properties.</param>
    /// <returns>The thresholds, or <see langword="null"/>.</returns>
    public static WindowsContentFilterOptions? GetWindowsContentFilter(this AdditionalPropertiesDictionary? properties) =>
        WindowsOptionsExtensions.GetWindowsContentFilter(properties);

    /// <summary>Converts content-filter thresholds to the native options.</summary>
    /// <param name="options">The thresholds.</param>
    /// <returns>The native options, or <see langword="null"/> if <paramref name="options"/> is <see langword="null"/>.</returns>
    public static ContentFilterOptions? ToNative(this WindowsContentFilterOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        var native = new ContentFilterOptions();
        if (options.PromptMaxSeverity is { } prompt)
        {
            native.PromptMaxAllowedSeverityLevel = new TextContentFilterSeverity(ToNative(prompt));
        }

        if (options.ResponseMaxSeverity is { } response)
        {
            native.ResponseMaxAllowedSeverityLevel = new TextContentFilterSeverity(ToNative(response));
        }

        if (options.ImageMaxSeverity is { } image)
        {
            native.ImageMaxAllowedSeverityLevel = new ImageContentFilterSeverity(ToNative(image));
        }

        return native;
    }

    /// <summary>
    /// Throws the shared exception for a failed status. Returns normally for <c>Complete</c> and for
    /// <c>ResponseBlockedByContentModeration</c> when <paramref name="responseBlockedIsFinishReason"/> is set
    /// (chat reports it as <c>ChatFinishReason.ContentFilter</c>).
    /// </summary>
    /// <param name="status">The native status.</param>
    /// <param name="extendedError">The native extended error.</param>
    /// <param name="modelId">The model identifier.</param>
    /// <param name="responseBlockedIsFinishReason">Whether a blocked response is reported as a finish reason instead of an exception.</param>
    public static void ThrowIfFailed(LanguageModelResponseStatus status, Exception? extendedError, string modelId, bool responseBlockedIsFinishReason = false)
    {
        switch (status)
        {
            case LanguageModelResponseStatus.Complete:
                return;
            case LanguageModelResponseStatus.ResponseBlockedByContentModeration when responseBlockedIsFinishReason:
                return;
            case LanguageModelResponseStatus.ResponseBlockedByContentModeration:
                throw new LocalModelContentFilteredException("The response was blocked by content moderation.", extendedError) { ModelId = modelId };
            case LanguageModelResponseStatus.PromptBlockedByContentModeration:
                throw new LocalModelContentFilteredException("The prompt was blocked by content moderation.", extendedError) { ModelId = modelId, IsInputFiltered = true };
            case LanguageModelResponseStatus.PromptLargerThanContext:
                throw new LocalModelContextLengthExceededException("The prompt is larger than the model's context.", extendedError) { ModelId = modelId };
            case LanguageModelResponseStatus.BlockedByPolicy:
                throw new LocalModelNotSupportedException(new ModelAvailability(ModelAvailabilityStatus.DisabledByPolicy, "The request was blocked by policy."), modelId);
            default:
                throw new LocalModelException($"The model failed with status {status}. {extendedError?.Message}", extendedError) { ModelId = modelId };
        }
    }

    /// <summary>Maps a structured-output status to the equivalent response status.</summary>
    /// <param name="status">The structured-output status.</param>
    /// <returns>The response status.</returns>
    public static LanguageModelResponseStatus ToResponseStatus(this GenerateStructuredJsonResponseStatus status) => status switch
    {
        GenerateStructuredJsonResponseStatus.Complete or GenerateStructuredJsonResponseStatus.CompleteWithInvalidStructure => LanguageModelResponseStatus.Complete,
        GenerateStructuredJsonResponseStatus.InProgress => LanguageModelResponseStatus.InProgress,
        GenerateStructuredJsonResponseStatus.BlockedByPolicy => LanguageModelResponseStatus.BlockedByPolicy,
        GenerateStructuredJsonResponseStatus.PromptLargerThanContext => LanguageModelResponseStatus.PromptLargerThanContext,
        GenerateStructuredJsonResponseStatus.PromptBlockedByContentModeration => LanguageModelResponseStatus.PromptBlockedByContentModeration,
        GenerateStructuredJsonResponseStatus.ResponseBlockedByContentModeration => LanguageModelResponseStatus.ResponseBlockedByContentModeration,
        _ => LanguageModelResponseStatus.Error,
    };

    private static SeverityLevel ToNative(WindowsContentSeverity severity) => severity switch
    {
        WindowsContentSeverity.Minimum => SeverityLevel.Minimum,
        WindowsContentSeverity.Low => SeverityLevel.Low,
        WindowsContentSeverity.Medium => SeverityLevel.Medium,
        _ => SeverityLevel.High,
    };
}
