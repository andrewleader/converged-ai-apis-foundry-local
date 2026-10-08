using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.AI.Local.Foundry.Runtime;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.Logging;

namespace Microsoft.AI.Local.Foundry.Providers;

/// <summary>A Foundry handle of any task type; lets <see cref="FoundryModelExtensions.WithDevice"/> create device-specific siblings.</summary>
internal interface IFoundryModelHandle : ILocalModel
{
    LocalModelDescriptor Descriptor { get; }

    LocalDevice Device { get; }

    ILocalModel WithDevice(LocalDevice device);
}

/// <summary>
/// The base class of Foundry Local model handles. It implements the acquisition shared by every Foundry model
/// (initialize Foundry Local → register execution providers → download → load), device variants, and
/// reference-counted unloading across clients (see <see cref="FoundryUnloadPolicy"/>).
/// </summary>
/// <remarks>This type is intended for Foundry task provider packages. App code uses the <see cref="ILocalModel"/> interfaces.</remarks>
/// <typeparam name="TClient">The inference client contract.</typeparam>
public abstract partial class FoundryModelHandle<TClient> : LocalModelBase<TClient>, IFoundryModelHandle
    where TClient : class
{
    private static readonly ConcurrentDictionary<(string Id, LocalDevice Device), FoundryModelHandle<TClient>> Handles = new();

    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly object _leaseGate = new();
    private IFoundryModelVariantLifecycle? _variant;
    private int _clients;
    private CancellationTokenSource? _idleUnload;

    /// <summary>Initializes a new instance of the <see cref="FoundryModelHandle{TClient}"/> class.</summary>
    /// <param name="descriptor">The model descriptor from the Foundry catalog manifest.</param>
    /// <param name="device">The device the handle is bound to; <see cref="LocalDevice.Auto"/> lets Foundry Local choose.</param>
    protected FoundryModelHandle(LocalModelDescriptor descriptor, LocalDevice device)
        : base(
            FormatId((descriptor ?? throw new ArgumentNullException(nameof(descriptor))).Id, device),
            FormatName(descriptor.DisplayName, device),
            descriptor.ProviderName,
            descriptor.Capabilities)
    {
        Descriptor = descriptor;
        Device = device;
    }

    /// <summary>Gets the model descriptor.</summary>
    public LocalModelDescriptor Descriptor { get; }

    /// <summary>Gets the device the handle is bound to.</summary>
    public LocalDevice Device { get; }

    /// <inheritdoc/>
    protected override IReadOnlyList<KeyValuePair<AcquisitionStage, double>> AcquisitionPlan { get; } =
    [
        new(AcquisitionStage.Preparing, 0.05),
        new(AcquisitionStage.DownloadingRuntime, 0.15),
        new(AcquisitionStage.DownloadingModel, 0.7),
        new(AcquisitionStage.Loading, 0.1),
    ];

    private static IFoundryRuntime Runtime => FoundryProvider.Runtime;

    /// <summary>Returns the handle for a specific device variant of this model. Handles are cached per process.</summary>
    /// <param name="device">The device.</param>
    /// <returns>The handle.</returns>
    public ILocalModel WithDevice(LocalDevice device) => device == Device ? this : GetOrCreate(Descriptor, device, CreateForDevice);

    /// <summary>Returns the process-wide handle for a model and device, creating it with <paramref name="create"/> on first use.</summary>
    /// <typeparam name="THandle">The concrete handle type.</typeparam>
    /// <param name="descriptor">The model descriptor.</param>
    /// <param name="device">The device.</param>
    /// <param name="create">Creates the handle.</param>
    /// <returns>The handle.</returns>
    protected static THandle GetOrCreate<THandle>(LocalModelDescriptor descriptor, LocalDevice device, Func<LocalModelDescriptor, LocalDevice, THandle> create)
        where THandle : FoundryModelHandle<TClient>
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(create);
        return (THandle)Handles.GetOrAdd((descriptor.Id, device), static (key, state) => state.create(state.descriptor, key.Device), (descriptor, create));
    }

    /// <summary>Creates a handle of the same model bound to <paramref name="device"/>.</summary>
    /// <param name="descriptor">The model descriptor.</param>
    /// <param name="device">The device.</param>
    /// <returns>The new handle.</returns>
    protected abstract FoundryModelHandle<TClient> CreateForDevice(LocalModelDescriptor descriptor, LocalDevice device);

    /// <summary>Creates the client for a loaded variant. The client must dispose <paramref name="lease"/> when it's disposed.</summary>
    /// <param name="variant">The loaded variant.</param>
    /// <param name="lease">Keeps the model loaded while the client is alive.</param>
    /// <returns>The client.</returns>
    protected abstract TClient CreateClient(IFoundryModelVariant variant, IDisposable lease);

    /// <inheritdoc/>
    protected override async ValueTask<ModelAvailability> GetAvailabilityCoreAsync(CancellationToken cancellationToken)
    {
        if (CheckStatic() is { } unavailable)
        {
            return unavailable;
        }

        if (await InitializeAsync(cancellationToken).ConfigureAwait(false) is { } initFailure)
        {
            return initFailure;
        }

        var (variant, missing) = await ResolveVariantAsync(cancellationToken).ConfigureAwait(false);
        if (variant is null)
        {
            return missing!;
        }

        if (await variant.IsLoadedAsync(cancellationToken).ConfigureAwait(false))
        {
            return ModelAvailability.Ready;
        }

        return await variant.IsCachedAsync(cancellationToken).ConfigureAwait(false)
            ? new ModelAvailability(ModelAvailabilityStatus.NotReady, "The model is downloaded and needs to be loaded.")
            : new ModelAvailability(ModelAvailabilityStatus.NotReady, "The model needs to be downloaded.");
    }

    /// <inheritdoc/>
    protected override async Task<ModelAvailability> EnsureReadyCoreAsync(AcquisitionProgressReporter progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        progress.Report(AcquisitionStage.Preparing, 0);
        if (CheckStatic() is { } unavailable)
        {
            return unavailable;
        }

        try
        {
            // Failures that might be transient (e.g. network) surface as exceptions; the next call retries.
            await Runtime.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (FoundryErrors.IsPlatformFailure(ex))
        {
            return FoundryErrors.PlatformUnavailable(ex);
        }

        progress.Report(AcquisitionStage.Preparing, 1);

        await Runtime.EnsureExecutionProvidersAsync(
            FoundryProvider.Options.ExecutionProviders,
            p => progress.Report(AcquisitionStage.DownloadingRuntime, p),
            cancellationToken).ConfigureAwait(false);
        progress.Report(AcquisitionStage.DownloadingRuntime, 1);

        // Resolve after EP registration: newly registered EPs can change the best variant.
        var (variant, missing) = await ResolveVariantAsync(cancellationToken, refresh: true).ConfigureAwait(false);
        if (variant is null)
        {
            return missing!;
        }

        if (!await variant.IsCachedAsync(cancellationToken).ConfigureAwait(false))
        {
            Log.Downloading(Logger, Id, variant.Id);
            await variant.DownloadAsync(p => progress.Report(AcquisitionStage.DownloadingModel, p), cancellationToken).ConfigureAwait(false);
        }

        progress.Report(AcquisitionStage.DownloadingModel, 1);
        await EnsureLoadedAsync(variant, cancellationToken).ConfigureAwait(false);
        progress.Report(AcquisitionStage.Loading, 1);
        return ModelAvailability.Ready;
    }

    /// <inheritdoc/>
    protected sealed override async Task<TClient> CreateClientCoreAsync(CancellationToken cancellationToken)
    {
        var variant = Volatile.Read(ref _variant) ?? throw new LocalModelNotReadyException($"The model '{Id}' is not ready.") { ModelId = Id };
        var lease = AcquireLease(variant);
        try
        {
            // The model may have been unloaded by the unload policy between EnsureReadyAsync and here.
            await EnsureLoadedAsync(variant, cancellationToken).ConfigureAwait(false);
            return CreateClient(variant, lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static string FormatId(string id, LocalDevice device) =>
        device == LocalDevice.Auto ? id : $"{id}@{device.ToString().ToLowerInvariant()}";

    private static string FormatName(string name, LocalDevice device) =>
        device == LocalDevice.Auto ? name : $"{name} ({device.ToString().ToUpperInvariant()})";

    private ModelAvailability? CheckStatic()
    {
        if (Descriptor.RetiredMessage is { } retired)
        {
            return new ModelAvailability(ModelAvailabilityStatus.Retired, retired);
        }

        var rid = FoundryPlatform.CurrentRuntimeIdentifier;
        if (!Descriptor.Platforms.Contains(rid, StringComparer.OrdinalIgnoreCase))
        {
            return new ModelAvailability(
                ModelAvailabilityStatus.NotSupportedOnPlatform,
                $"'{Descriptor.DisplayName}' isn't supported on {rid} (supported: {string.Join(", ", Descriptor.Platforms)}).");
        }

        return null;
    }

    /// <summary>Initializes the runtime. Returns a non-ready availability on failure.</summary>
    private async Task<ModelAvailability?> InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Runtime.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (FoundryErrors.IsPlatformFailure(ex))
        {
            return FoundryErrors.PlatformUnavailable(ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.InitializationFailed(Logger, ex);
            return new ModelAvailability(ModelAvailabilityStatus.NotReady, $"Foundry Local could not be initialized: {ex.Message}");
        }
    }

    private async Task<(IFoundryModelVariantLifecycle? Variant, ModelAvailability? Unavailable)> ResolveVariantAsync(CancellationToken cancellationToken, bool refresh = false)
    {
        if (!refresh && Volatile.Read(ref _variant) is { } cached)
        {
            return (cached, null);
        }

        var model = await Runtime.GetModelAsync(Descriptor.Alias, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return (null, new ModelAvailability(
                ModelAvailabilityStatus.NotSupportedOnDevice,
                $"The Foundry Local catalog has no '{Descriptor.Alias}' model for this device."));
        }

        IFoundryModelVariantLifecycle? variant = Device == LocalDevice.Auto
            ? model.DefaultVariant
            : model.Variants.FirstOrDefault(v => v.Device == Device);

        if (variant is null)
        {
            var devices = string.Join(", ", model.Variants.Select(v => v.Device).Distinct());
            return (null, new ModelAvailability(
                ModelAvailabilityStatus.NotSupportedOnDevice,
                $"'{Descriptor.Alias}' has no {Device.ToString().ToUpperInvariant()} variant on this device (available: {devices})."));
        }

        Volatile.Write(ref _variant, variant);
        return (variant, null);
    }

    private async Task EnsureLoadedAsync(IFoundryModelVariantLifecycle variant, CancellationToken cancellationToken)
    {
        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!await variant.IsLoadedAsync(cancellationToken).ConfigureAwait(false))
            {
                Log.Loading(Logger, Id, variant.Id);
                await variant.LoadAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private Lease AcquireLease(IFoundryModelVariantLifecycle variant)
    {
        lock (_leaseGate)
        {
            _clients++;
            _idleUnload?.Cancel();
            _idleUnload?.Dispose();
            _idleUnload = null;
        }

        return new Lease(this, variant);
    }

    private void ReleaseLease(IFoundryModelVariantLifecycle variant)
    {
        var policy = FoundryProvider.Options.UnloadPolicy;
        CancellationTokenSource? idle = null;
        lock (_leaseGate)
        {
            if (--_clients > 0 || policy.Mode == FoundryUnloadMode.Never)
            {
                return;
            }

            if (policy.Mode == FoundryUnloadMode.Idle)
            {
                idle = _idleUnload = new CancellationTokenSource();
            }
        }

        _ = UnloadWhenUnusedAsync(variant, policy.Mode == FoundryUnloadMode.Idle ? policy.IdleTimeout : TimeSpan.Zero, idle?.Token ?? CancellationToken.None);
    }

    private async Task UnloadWhenUnusedAsync(IFoundryModelVariantLifecycle variant, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (Volatile.Read(ref _clients) == 0 && await variant.IsLoadedAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    Log.Unloading(Logger, Id);
                    await variant.UnloadAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                _loadGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // A new client arrived before the idle timeout.
        }
        catch (Exception ex)
        {
            Log.UnloadFailed(Logger, Id, ex);
        }
    }

    private sealed class Lease(FoundryModelHandle<TClient> owner, IFoundryModelVariantLifecycle variant) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.ReleaseLease(variant);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(100, LogLevel.Information, "Downloading model '{ModelId}' (variant '{VariantId}').")]
        public static partial void Downloading(ILogger logger, string modelId, string variantId);

        [LoggerMessage(101, LogLevel.Information, "Loading model '{ModelId}' (variant '{VariantId}').")]
        public static partial void Loading(ILogger logger, string modelId, string variantId);

        [LoggerMessage(102, LogLevel.Information, "Unloading model '{ModelId}': no clients are using it.")]
        public static partial void Unloading(ILogger logger, string modelId);

        [LoggerMessage(103, LogLevel.Warning, "Unloading model '{ModelId}' failed.")]
        public static partial void UnloadFailed(ILogger logger, string modelId, Exception exception);

        [LoggerMessage(104, LogLevel.Warning, "Foundry Local initialization failed.")]
        public static partial void InitializationFailed(ILogger logger, Exception exception);
    }
}

/// <summary>Maps the current process to a Foundry Local runtime identifier.</summary>
internal static class FoundryPlatform
{
    public static string CurrentRuntimeIdentifier { get; } = Compute();

    private static string Compute()
    {
        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsLinux() ? "linux" : "unknown";
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            var other => other.ToString().ToLowerInvariant(),
        };
        return $"{os}-{arch}";
    }
}
