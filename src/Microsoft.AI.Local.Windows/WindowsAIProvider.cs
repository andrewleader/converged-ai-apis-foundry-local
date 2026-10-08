namespace Microsoft.AI.Local.Windows;

/// <summary>
/// Optional, process-wide configuration of the Windows inbox provider.
/// </summary>
/// <remarks>
/// Most apps need no code here: the Limited Access Feature unlock for Phi Silica is configured with the MSBuild
/// properties <c>WindowsAILimitedAccessFeatureId</c>, <c>WindowsAILimitedAccessFeatureToken</c> and
/// <c>WindowsAILimitedAccessFeatureAttestation</c>, which generate a <see cref="WindowsAILimitedAccessFeatureAttribute"/>.
/// </remarks>
public static class WindowsAIProvider
{
    /// <summary>The provider name reported by Windows inbox models and their clients.</summary>
    public const string ProviderName = "Windows";

    private static readonly object Gate = new();
    private static WindowsAIProviderOptions options = new();

    /// <summary>Gets the current options.</summary>
    public static WindowsAIProviderOptions Options
    {
        get
        {
            lock (Gate)
            {
                return options;
            }
        }
    }

    /// <summary>Configures the provider. Call it at startup, before using any Windows model handle.</summary>
    /// <param name="configure">A callback that edits a copy of the current options.</param>
    public static void Configure(Action<WindowsAIProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        lock (Gate)
        {
            var copy = options with { };
            configure(copy);
            options = copy;
        }
    }
}

/// <summary>Options of the Windows inbox provider.</summary>
public sealed record WindowsAIProviderOptions
{
    /// <summary>
    /// Gets or sets the Limited Access Feature used to unlock Phi Silica and the text skills built on it.
    /// When <see langword="null"/>, the provider uses the <see cref="WindowsAILimitedAccessFeatureAttribute"/> of the
    /// entry assembly, if present.
    /// </summary>
    public WindowsAILimitedAccessFeature? LimitedAccessFeature { get; set; }
}

/// <summary>A Limited Access Feature (LAF) registration.</summary>
/// <param name="FeatureId">The feature identifier, for example <c>com.microsoft.windows.ai.languagemodel</c>.</param>
/// <param name="Token">The token issued to the app.</param>
/// <param name="Attestation">The attestation string issued with the token.</param>
public sealed record WindowsAILimitedAccessFeature(string FeatureId, string Token, string Attestation);

/// <summary>
/// Declares the Limited Access Feature used to unlock Phi Silica. Generated from the
/// <c>WindowsAILimitedAccessFeatureId</c>/<c>Token</c>/<c>Attestation</c> MSBuild properties; you don't need to
/// apply it by hand.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class WindowsAILimitedAccessFeatureAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="WindowsAILimitedAccessFeatureAttribute"/> class.</summary>
    /// <param name="featureId">The feature identifier.</param>
    /// <param name="token">The token issued to the app.</param>
    /// <param name="attestation">The attestation string issued with the token.</param>
    public WindowsAILimitedAccessFeatureAttribute(string featureId, string token, string attestation)
    {
        FeatureId = featureId;
        Token = token;
        Attestation = attestation;
    }

    /// <summary>Gets the feature identifier.</summary>
    public string FeatureId { get; }

    /// <summary>Gets the token.</summary>
    public string Token { get; }

    /// <summary>Gets the attestation string.</summary>
    public string Attestation { get; }
}
