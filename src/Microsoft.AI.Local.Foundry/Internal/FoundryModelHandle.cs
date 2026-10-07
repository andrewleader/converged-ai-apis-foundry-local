using System.Runtime.InteropServices;
using Microsoft.AI.Local.Foundry.Runtime;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Microsoft.AI.Local.Foundry;

/// <summary>A Foundry handle; lets <see cref="FoundryModelExtensions.WithDevice"/> create device-specific siblings.</summary>
internal interface IFoundryModelHandle : ILocalModel
{
    FoundryModelDescriptor Descriptor { get; }

    LocalDevice Device { get; }

    ILocalModel WithDevice(LocalDevice device);
}

/// <summary>
/// The acquisition logic shared by every Foundry model: initialize Foundry Local → register execution providers →
/// download → load. Loaded models are reference-counted across clients (see <see cref="FoundryUnloadPolicy"/>).
/// </summary>
internal abstract partial class FoundryModelHandle<TClient> : LocalModelBase<TClient>, IFoundryModelHandle
    where TClient : class
{
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly object _leaseGate = new();
    private IFoundryModelVariant? _variant;
    private int _clients;
    private CancellationTokenSource? _idleUnload;

    protected FoundryModelHandle(FoundryModelDescriptor descriptor, LocalDevice device)
        : base(FormatId(descriptor.Alias, device), FormatName(descriptor.DisplayName, device), FoundryProvider.ProviderName, descriptor.Capabilities)
    {
        Descriptor = descriptor;
        Device = device;
    }

    public FoundryModelDescriptor Descriptor { get; }

    public LocalDevice Device { get; }

    protected override IReadOnlyList<KeyValuePair<AcquisitionStage, double>> AcquisitionPlan { get; } =
    [
        new(AcquisitionStage.Preparing, 0.05),
        new(AcquisitionStage.DownloadingRuntime, 0.15),
        new(AcquisitionStage.DownloadingModel, 0.7),
        new(AcquisitionStage.Loading, 0.1),
    ];

    private static IFoundryRuntime Runtime => FoundryProvider.Runtime;

    public ILocalModel WithDevice(LocalDevice device) => FoundryModelFactory.Get(Descriptor, device);

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

    protected override async Task<ModelAvailability> EnsureReadyCoreAsync(AcquisitionProgressReporter progress, CancellationToken cancellationToken)
    {
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

    /// <summary>Creates the MEAI client. The client must dispose <paramref name="lease"/> when it's disposed.</summary>
    protected abstract TClient CreateClient(IFoundryModelVariant variant, IDisposable lease);

    private static string FormatId(string alias, LocalDevice device) =>
        device == LocalDevice.Auto ? alias : $"{alias}@{device.ToString().ToLowerInvariant()}";

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

    private async Task<(IFoundryModelVariant? Variant, ModelAvailability? Unavailable)> ResolveVariantAsync(CancellationToken cancellationToken, bool refresh = false)
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

        IFoundryModelVariant? variant = Device == LocalDevice.Auto
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

    private async Task EnsureLoadedAsync(IFoundryModelVariant variant, CancellationToken cancellationToken)
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

    private Lease AcquireLease(IFoundryModelVariant variant)
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

    private void ReleaseLease(IFoundryModelVariant variant)
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

    private async Task UnloadWhenUnusedAsync(IFoundryModelVariant variant, TimeSpan delay, CancellationToken cancellationToken)
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

    private sealed class Lease(FoundryModelHandle<TClient> owner, IFoundryModelVariant variant) : IDisposable
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

internal sealed class FoundryTextGenerationModel(FoundryModelDescriptor descriptor, LocalDevice device)
    : FoundryModelHandle<IChatClient>(descriptor, device), ITextGenerationModel
{
    protected override IChatClient CreateClient(IFoundryModelVariant variant, IDisposable lease) =>
        new FoundryChatClient(this, variant, lease);
}

internal sealed class FoundryTextEmbeddingModel(FoundryModelDescriptor descriptor, LocalDevice device)
    : FoundryModelHandle<IEmbeddingGenerator<string, Embedding<float>>>(descriptor, device), ITextEmbeddingModel
{
    protected override IEmbeddingGenerator<string, Embedding<float>> CreateClient(IFoundryModelVariant variant, IDisposable lease) =>
        new FoundryEmbeddingGenerator(this, variant, lease);
}

internal sealed class FoundrySpeechToTextModel(FoundryModelDescriptor descriptor, LocalDevice device)
    : FoundryModelHandle<ISpeechToTextClient>(descriptor, device), ISpeechToTextModel
{
    protected override ISpeechToTextClient CreateClient(IFoundryModelVariant variant, IDisposable lease) =>
        new FoundrySpeechToTextClient(this, variant, lease);
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
