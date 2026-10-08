using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Windows;

/// <summary>
/// Content-moderation thresholds for Windows inbox models. Other providers ignore these settings.
/// </summary>
/// <remarks>Apply them with <see cref="WindowsOptionsExtensions.WithWindowsContentFilter(ChatOptions, WindowsContentFilterOptions)"/>.</remarks>
public sealed record WindowsContentFilterOptions
{
    /// <summary>Gets the maximum allowed severity of the prompt (all categories). <see langword="null"/> uses the OS default.</summary>
    public WindowsContentSeverity? PromptMaxSeverity { get; init; }

    /// <summary>Gets the maximum allowed severity of the response (all categories). <see langword="null"/> uses the OS default.</summary>
    public WindowsContentSeverity? ResponseMaxSeverity { get; init; }

    /// <summary>Gets the maximum allowed severity of input images (all categories). <see langword="null"/> uses the OS default.</summary>
    public WindowsContentSeverity? ImageMaxSeverity { get; init; }
}

/// <summary>A content severity threshold. Content above the threshold is blocked.</summary>
public enum WindowsContentSeverity
{
    /// <summary>The strictest threshold.</summary>
    Minimum,

    /// <summary>Blocks medium and higher severity content.</summary>
    Low,

    /// <summary>Blocks high severity content.</summary>
    Medium,

    /// <summary>The least restrictive threshold.</summary>
    High,
}

/// <summary>Typed, Windows-specific extensions for request options.</summary>
public static class WindowsOptionsExtensions
{
    internal const string ContentFilterKey = "Microsoft.AI.Local.Windows.ContentFilter";

    /// <summary>Sets Windows content-moderation thresholds on chat options. Other providers ignore them.</summary>
    /// <param name="options">The chat options.</param>
    /// <param name="contentFilter">The thresholds.</param>
    /// <returns><paramref name="options"/>, for chaining.</returns>
    public static ChatOptions WithWindowsContentFilter(this ChatOptions options, WindowsContentFilterOptions contentFilter)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contentFilter);
        (options.AdditionalProperties ??= [])[ContentFilterKey] = contentFilter;
        return options;
    }

    /// <summary>Sets Windows content-moderation thresholds on request options. Other providers ignore them.</summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <param name="options">The request options.</param>
    /// <param name="contentFilter">The thresholds.</param>
    /// <returns><paramref name="options"/>, for chaining.</returns>
    public static TOptions WithWindowsContentFilter<TOptions>(this TOptions options, WindowsContentFilterOptions contentFilter)
        where TOptions : LocalAIRequestOptions
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contentFilter);
        (options.AdditionalProperties ??= [])[ContentFilterKey] = contentFilter;
        return options;
    }

    internal static WindowsContentFilterOptions? GetWindowsContentFilter(this AdditionalPropertiesDictionary? properties) =>
        properties is not null && properties.TryGetValue(ContentFilterKey, out var value) ? value as WindowsContentFilterOptions : null;
}
