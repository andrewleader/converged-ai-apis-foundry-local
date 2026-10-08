using Microsoft.AI.Foundry.Local;

namespace Microsoft.AI.Local.Foundry.Providers;

/// <summary>Maps Foundry Local failures to the shared exception hierarchy and availability statuses.</summary>
/// <remarks>This type is intended for Foundry task provider packages.</remarks>
public static class FoundryErrors
{
    /// <summary>Gets a value indicating whether <paramref name="exception"/> means Foundry Local can't run in this process at all.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> for missing or incompatible native binaries.</returns>
    public static bool IsPlatformFailure(Exception exception) => exception is
        DllNotFoundException or
        EntryPointNotFoundException or
        BadImageFormatException or
        PlatformNotSupportedException or
        TypeInitializationException { InnerException: DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or PlatformNotSupportedException };

    /// <summary>Returns the availability reported when Foundry Local can't run in this process.</summary>
    /// <param name="exception">The platform failure.</param>
    /// <returns>A <see cref="ModelAvailabilityStatus.NotSupportedOnPlatform"/> availability.</returns>
    public static ModelAvailability PlatformUnavailable(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new(
            ModelAvailabilityStatus.NotSupportedOnPlatform,
            $"Foundry Local can't run in this process ({exception.GetType().Name}: {exception.Message}). Check that the app's runtime identifier is supported (win-x64, win-arm64, osx-arm64, linux-x64, linux-arm64).");
    }

    /// <summary>Wraps a Foundry Local SDK exception into the shared exception hierarchy.</summary>
    /// <param name="exception">The exception.</param>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>The exception to throw; <paramref name="exception"/> itself when it needs no wrapping.</returns>
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
