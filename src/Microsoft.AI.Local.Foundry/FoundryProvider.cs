using Microsoft.AI.Local.Foundry.Runtime;
using Microsoft.Extensions.Logging;

namespace Microsoft.AI.Local.Foundry;

/// <summary>
/// Optional, process-wide configuration of the Foundry Local provider.
/// </summary>
/// <remarks>
/// No setup code is required: the provider creates the Foundry Local manager lazily and thread-safely the first time a
/// Foundry model handle does I/O, with <c>AppName</c> defaulting to the entry assembly name. If the app
/// already created <c>FoundryLocalManager</c> itself, the provider reuses that instance and ignores the
/// manager-related options.
/// </remarks>
public static class FoundryProvider
{
    /// <summary>The provider name reported by Foundry handles and clients.</summary>
    public const string ProviderName = "Foundry";

    private static readonly object Gate = new();
    private static FoundryProviderOptions options = new();
    private static IFoundryRuntime? runtime;
    private static bool runtimeUsed;

    /// <summary>Gets the current options.</summary>
    public static FoundryProviderOptions Options
    {
        get
        {
            lock (Gate)
            {
                return options;
            }
        }
    }

    /// <summary>
    /// Configures the provider. Call it at startup, before any Foundry model handle does I/O; the
    /// manager-related options (<see cref="FoundryProviderOptions.AppName"/>, directories, logging) can't change
    /// after the Foundry Local manager has been created.
    /// </summary>
    /// <param name="configure">A callback that edits a copy of the current options.</param>
    /// <exception cref="InvalidOperationException">The manager-related options changed after the runtime was initialized.</exception>
    public static void Configure(Action<FoundryProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        lock (Gate)
        {
            var copy = options with { };
            configure(copy);
            if (runtimeUsed && !copy.HasSameManagerSettings(options))
            {
                throw new InvalidOperationException(
                    "The Foundry Local manager has already been created; AppName, directories, catalog and logging options can't change. " +
                    "Call FoundryProvider.Configure at startup, before using a Foundry model handle.");
            }

            options = copy;
        }
    }

    internal static IFoundryRuntime Runtime
    {
        get
        {
            lock (Gate)
            {
                runtimeUsed = true;
                return runtime ??= new FoundryLocalRuntime(() => Options);
            }
        }
    }

    /// <summary>Replaces the runtime (tests only).</summary>
    internal static void SetRuntimeForTesting(IFoundryRuntime? testRuntime)
    {
        lock (Gate)
        {
            runtime = testRuntime;
            runtimeUsed = false;
            options = new();
        }
    }
}

/// <summary>Options of the Foundry Local provider.</summary>
public sealed record FoundryProviderOptions
{
    /// <summary>Gets or sets the application name Foundry Local uses for its data directory. Defaults to the entry assembly name.</summary>
    public string? AppName { get; set; }

    /// <summary>Gets or sets the Foundry Local application data directory. Defaults to Foundry Local's default.</summary>
    public string? AppDataDirectory { get; set; }

    /// <summary>Gets or sets the model cache directory. Defaults to Foundry Local's default, which is shared across apps.</summary>
    public string? ModelCacheDirectory { get; set; }

    /// <summary>Gets or sets the Foundry Local log directory.</summary>
    public string? LogsDirectory { get; set; }

    /// <summary>Gets or sets the catalog region, for example <c>westus</c>. Defaults to Foundry Local's default.</summary>
    public string? CatalogRegion { get; set; }

    /// <summary>
    /// Gets or sets the logger factory that receives Foundry Local SDK logs. Defaults to
    /// <see cref="LocalAIOptions.LoggerFactory"/>.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Gets or sets which execution providers (EPs) to download and register during
    /// <see cref="ILocalModel.EnsureReadyAsync"/>. Defaults to <see cref="FoundryExecutionProviders.Auto"/>.
    /// </summary>
    public FoundryExecutionProviders ExecutionProviders { get; set; } = FoundryExecutionProviders.Auto;

    /// <summary>
    /// Gets or sets when a loaded model is unloaded from memory. Defaults to
    /// <see cref="FoundryUnloadPolicy.OnLastClientDisposed"/>.
    /// </summary>
    public FoundryUnloadPolicy UnloadPolicy { get; set; } = FoundryUnloadPolicy.OnLastClientDisposed;

    internal bool HasSameManagerSettings(FoundryProviderOptions other) =>
        AppName == other.AppName &&
        AppDataDirectory == other.AppDataDirectory &&
        ModelCacheDirectory == other.ModelCacheDirectory &&
        LogsDirectory == other.LogsDirectory &&
        CatalogRegion == other.CatalogRegion &&
        ReferenceEquals(LoggerFactory, other.LoggerFactory);
}

/// <summary>Which hardware execution providers (EPs) Foundry Local downloads and registers.</summary>
public sealed class FoundryExecutionProviders : IEquatable<FoundryExecutionProviders>
{
    private FoundryExecutionProviders(FoundryExecutionProviderMode mode, IReadOnlyList<string> names)
    {
        Mode = mode;
        Names = names;
    }

    /// <summary>
    /// Gets the default policy: download and register every EP available for this device (for example the NPU and GPU
    /// EPs on Windows). EP failures are logged and the model falls back to the variants that can run.
    /// </summary>
    public static FoundryExecutionProviders Auto { get; } = new(FoundryExecutionProviderMode.Auto, []);

    /// <summary>Gets a policy that never downloads EPs; only already-registered EPs (and the CPU) are used.</summary>
    public static FoundryExecutionProviders None { get; } = new(FoundryExecutionProviderMode.None, []);

    /// <summary>Gets the policy mode.</summary>
    public FoundryExecutionProviderMode Mode { get; }

    /// <summary>Gets the EP names for <see cref="FoundryExecutionProviderMode.Explicit"/>.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>Creates a policy that downloads and registers exactly the named EPs. A failure fails acquisition.</summary>
    /// <param name="names">The EP names, as reported by Foundry Local (for example <c>QNNExecutionProvider</c>).</param>
    /// <returns>The policy.</returns>
    public static FoundryExecutionProviders Explicit(params IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var list = names.ToArray();
        if (list.Length == 0 || list.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Specify at least one non-empty execution provider name.", nameof(names));
        }

        return new(FoundryExecutionProviderMode.Explicit, list);
    }

    /// <inheritdoc />
    public bool Equals(FoundryExecutionProviders? other) =>
        other is not null && Mode == other.Mode && Names.SequenceEqual(other.Names, StringComparer.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as FoundryExecutionProviders);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Mode, Names.Count);

    /// <inheritdoc />
    public override string ToString() => Mode == FoundryExecutionProviderMode.Explicit ? $"Explicit({string.Join(", ", Names)})" : Mode.ToString();
}

/// <summary>The kind of <see cref="FoundryExecutionProviders"/> policy.</summary>
public enum FoundryExecutionProviderMode
{
    /// <summary>Download and register every available EP; failures are not fatal.</summary>
    Auto,

    /// <summary>Don't download EPs.</summary>
    None,

    /// <summary>Download and register the named EPs; failures are fatal.</summary>
    Explicit,
}

/// <summary>When the provider unloads a Foundry model from memory.</summary>
public sealed class FoundryUnloadPolicy
{
    private FoundryUnloadPolicy(FoundryUnloadMode mode, TimeSpan idleTimeout)
    {
        Mode = mode;
        IdleTimeout = idleTimeout;
    }

    /// <summary>Gets the default policy: unload when the last client created from the handle is disposed.</summary>
    public static FoundryUnloadPolicy OnLastClientDisposed { get; } = new(FoundryUnloadMode.OnLastClientDisposed, TimeSpan.Zero);

    /// <summary>Gets a policy that keeps models loaded until the process exits.</summary>
    public static FoundryUnloadPolicy Never { get; } = new(FoundryUnloadMode.Never, TimeSpan.Zero);

    /// <summary>Gets the policy mode.</summary>
    public FoundryUnloadMode Mode { get; }

    /// <summary>Gets the idle timeout for <see cref="FoundryUnloadMode.Idle"/>.</summary>
    public TimeSpan IdleTimeout { get; }

    /// <summary>Creates a policy that unloads a model once it has had no clients for <paramref name="timeout"/>.</summary>
    /// <param name="timeout">The idle time before unloading.</param>
    /// <returns>The policy.</returns>
    public static FoundryUnloadPolicy Idle(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        return new(FoundryUnloadMode.Idle, timeout);
    }

    /// <inheritdoc />
    public override string ToString() => Mode == FoundryUnloadMode.Idle ? $"Idle({IdleTimeout})" : Mode.ToString();
}

/// <summary>The kind of <see cref="FoundryUnloadPolicy"/>.</summary>
public enum FoundryUnloadMode
{
    /// <summary>Unload when the last client is disposed.</summary>
    OnLastClientDisposed,

    /// <summary>Never unload.</summary>
    Never,

    /// <summary>Unload after an idle timeout.</summary>
    Idle,
}
