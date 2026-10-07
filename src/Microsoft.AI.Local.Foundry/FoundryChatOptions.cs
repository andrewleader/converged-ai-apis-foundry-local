using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Foundry;

/// <summary>Foundry Local-specific generation settings, attached to <see cref="ChatOptions"/> with <see cref="FoundryOptionsExtensions.WithFoundry"/>.</summary>
public sealed record FoundryChatOptions
{
    /// <summary>Gets or sets whether to sample (<see langword="false"/> = greedy decoding).</summary>
    public bool? DoSample { get; set; }

    /// <summary>
    /// Gets additional native Foundry Local request parameters (for example <c>reasoning_effort</c>). Keys and values
    /// are passed through as-is; typed <see cref="ChatOptions"/> values win on conflicts.
    /// </summary>
    public IDictionary<string, string> AdditionalOptions { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>Attaches provider-specific options to the shared Microsoft.Extensions.AI options types.</summary>
public static class FoundryOptionsExtensions
{
    internal const string ChatOptionsKey = "Microsoft.AI.Local.Foundry.ChatOptions";

    /// <summary>
    /// Attaches Foundry Local settings to <paramref name="options"/>. Other providers ignore them, so the same
    /// options work when the model is switched.
    /// </summary>
    /// <param name="options">The chat options.</param>
    /// <param name="foundryOptions">The Foundry settings.</param>
    /// <returns><paramref name="options"/>, for chaining.</returns>
    public static ChatOptions WithFoundry(this ChatOptions options, FoundryChatOptions foundryOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(foundryOptions);
        (options.AdditionalProperties ??= [])[ChatOptionsKey] = foundryOptions;
        return options;
    }

    internal static FoundryChatOptions? GetFoundryOptions(this ChatOptions? options) =>
        options?.AdditionalProperties is { } properties && properties.TryGetValue(ChatOptionsKey, out var value) ? value as FoundryChatOptions : null;
}
