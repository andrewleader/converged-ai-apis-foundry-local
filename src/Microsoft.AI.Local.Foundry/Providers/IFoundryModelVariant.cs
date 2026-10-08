namespace Microsoft.AI.Local.Foundry.Providers;

/// <summary>
/// A loaded Foundry Local model variant (one device / execution provider / quantization), passed to the Foundry task
/// provider packages to create clients.
/// </summary>
/// <remarks>This type is intended for Foundry task provider packages. App code reaches the native model through <c>GetService</c>.</remarks>
public interface IFoundryModelVariant
{
    /// <summary>Gets the Foundry Local variant identifier, for example <c>Phi-4-mini-instruct-generic-cpu:1</c>.</summary>
    string Id { get; }

    /// <summary>Gets the device the variant runs on.</summary>
    LocalDevice Device { get; }

    /// <summary>Gets the execution provider the variant runs on, if known.</summary>
    string? ExecutionProvider { get; }

    /// <summary>Gets the native <c>Microsoft.AI.Foundry.Local.IModel</c>.</summary>
    object Native { get; }

    /// <summary>Gets the variant's context length in tokens, if known.</summary>
    int? ContextLength { get; }

    /// <summary>Gets the variant's maximum number of output tokens, if known.</summary>
    int? MaxOutputTokens { get; }

    /// <summary>Gets a value indicating whether the variant supports tool calling, if known.</summary>
    bool? SupportsToolCalling { get; }
}
