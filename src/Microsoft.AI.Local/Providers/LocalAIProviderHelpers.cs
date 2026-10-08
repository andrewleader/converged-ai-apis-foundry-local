using Microsoft.Extensions.Logging;

namespace Microsoft.AI.Local.Providers;

/// <summary>
/// Helpers for provider authors that implement consistent cross-provider behavior.
/// </summary>
public static class LocalAIProviderHelpers
{
    /// <summary>
    /// Handles a request option the provider can't honor: throws <see cref="LocalModelOptionNotSupportedException"/>
    /// if <see cref="LocalAIOptions.ThrowOnUnsupportedOptions"/> is enabled; otherwise logs at Debug level.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="optionName">The name of the option, for example <c>ChatOptions.StopSequences</c>.</param>
    /// <param name="logger">The logger to use. Defaults to a logger from <see cref="LocalAIOptions.LoggerFactory"/>.</param>
    /// <exception cref="LocalModelOptionNotSupportedException"><see cref="LocalAIOptions.ThrowOnUnsupportedOptions"/> is enabled.</exception>
    public static void ReportUnsupportedOption(string providerName, string optionName, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(optionName);

        if (LocalAIOptions.Default.ThrowOnUnsupportedOptions)
        {
            throw new LocalModelOptionNotSupportedException($"The {providerName} provider does not support the option '{optionName}'.")
            {
                OptionName = optionName,
            };
        }

        Log.UnsupportedOptionIgnored(logger ?? LocalAIOptions.Default.LoggerFactory.CreateLogger("Microsoft.AI.Local"), providerName, optionName);
    }
}
