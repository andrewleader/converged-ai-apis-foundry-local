using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local;

/// <summary>
/// Base class for the per-request options bags of the contracts defined by this library.
/// </summary>
/// <remarks>
/// Provider-specific settings are set through typed extension methods supplied by the provider package
/// (for example <c>options.WithWindowsContentFilter(...)</c>), which store their values in
/// <see cref="AdditionalProperties"/>. Settings a provider can't honor are ignored and logged, unless
/// <see cref="LocalAIOptions.ThrowOnUnsupportedOptions"/> is enabled.
/// </remarks>
public abstract class LocalAIRequestOptions
{
    /// <summary>Initializes a new instance of the <see cref="LocalAIRequestOptions"/> class.</summary>
    protected LocalAIRequestOptions()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalAIRequestOptions"/> class by copying another instance.</summary>
    /// <param name="other">The instance to copy.</param>
    protected LocalAIRequestOptions(LocalAIRequestOptions? other)
    {
        AdditionalProperties = other?.AdditionalProperties?.Clone();
    }

    /// <summary>Gets or sets additional, provider-specific properties.</summary>
    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }
}

/// <summary>
/// Base class for the results produced by the contracts defined by this library.
/// </summary>
public abstract class LocalAIResult
{
    /// <summary>Gets or sets the identifier of the model that produced the result.</summary>
    public string? ModelId { get; set; }

    /// <summary>Gets or sets the provider's native result object, if any.</summary>
    public object? RawRepresentation { get; set; }

    /// <summary>Gets or sets additional, provider-specific properties.</summary>
    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }
}
