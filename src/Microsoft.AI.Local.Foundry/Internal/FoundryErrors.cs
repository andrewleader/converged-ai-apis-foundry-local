using Microsoft.AI.Foundry.Local;

namespace Microsoft.AI.Local.Foundry;

internal static class FoundryErrors
{
    /// <summary>Gets a value indicating whether <paramref name="exception"/> means Foundry Local can't run in this process at all.</summary>
    public static bool IsPlatformFailure(Exception exception) => exception is
        DllNotFoundException or
        EntryPointNotFoundException or
        BadImageFormatException or
        PlatformNotSupportedException or
        TypeInitializationException { InnerException: DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or PlatformNotSupportedException };

    public static ModelAvailability PlatformUnavailable(Exception exception) => new(
        ModelAvailabilityStatus.NotSupportedOnPlatform,
        $"Foundry Local can't run in this process ({exception.GetType().Name}: {exception.Message}). Check that the app's runtime identifier is supported (win-x64, win-arm64, osx-arm64, linux-x64, linux-arm64).");

    /// <summary>Wraps a Foundry Local SDK exception into the shared exception hierarchy.</summary>
    public static Exception Wrap(Exception exception, string modelId) => exception switch
    {
        LocalModelException or OperationCanceledException or ArgumentException => exception,
        FoundryLocalException { ErrorCode: FoundryLocalErrorCode.OperationCancelled } => new OperationCanceledException(exception.Message, exception),
        FoundryLocalException fl when LooksLikeContextOverflow(fl) => new LocalModelContextLengthExceededException(fl.Message, fl) { ModelId = modelId },
        FoundryLocalException fl => new LocalModelException($"Foundry Local error: {fl.Message}", fl) { ModelId = modelId },
        _ => exception,
    };

    private static bool LooksLikeContextOverflow(FoundryLocalException exception) =>
        exception.Message.Contains("context length", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("max_length", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("too many tokens", StringComparison.OrdinalIgnoreCase);
}
