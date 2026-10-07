using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.AI.Local;

/// <summary>
/// Process-wide settings shared by all local AI providers.
/// </summary>
public sealed class LocalAIOptions
{
    /// <summary>Gets the process-wide settings.</summary>
    public static LocalAIOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether requests that set an option the provider can't honor
    /// (for example <c>ChatOptions.StopSequences</c> on a provider without stop sequences) throw
    /// <see cref="LocalModelOptionNotSupportedException"/>. The default is <see langword="false"/>: the option is
    /// ignored and a warning is logged.
    /// </summary>
    public bool ThrowOnUnsupportedOptions { get; set; }

    /// <summary>Gets or sets the logger factory used by providers. The default discards logs.</summary>
    public ILoggerFactory LoggerFactory
    {
        get;
        set => field = value ?? NullLoggerFactory.Instance;
    } = NullLoggerFactory.Instance;
}
