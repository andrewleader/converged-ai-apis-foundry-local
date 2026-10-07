using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Text;

namespace Microsoft.AI.Local.Windows;

/// <summary>Conversions between this library's types and the Windows AI API types.</summary>
internal static class WindowsInterop
{
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

    private static SeverityLevel ToNative(WindowsContentSeverity severity) => severity switch
    {
        WindowsContentSeverity.Minimum => SeverityLevel.Minimum,
        WindowsContentSeverity.Low => SeverityLevel.Low,
        WindowsContentSeverity.Medium => SeverityLevel.Medium,
        _ => SeverityLevel.High,
    };

    /// <summary>
    /// Throws the shared exception for a failed status. Returns normally for <c>Complete</c> and for
    /// <c>ResponseBlockedByContentModeration</c> when <paramref name="responseBlockedIsFinishReason"/> is set
    /// (chat reports it as <c>ChatFinishReason.ContentFilter</c>).
    /// </summary>
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
}
