using Microsoft.AI.Local.Providers;
using Microsoft.Windows.AI;
using Windows.Foundation;

namespace Microsoft.AI.Local.Windows;

/// <summary>The common acquisition logic of Windows inbox models (ready state → EnsureReadyAsync → CreateAsync).</summary>
internal abstract class WindowsModelBase<TClient> : LocalModelBase<TClient>
    where TClient : class
{
    private readonly bool _usesLanguageModel;

    protected WindowsModelBase(string id, string displayName, LocalModelCapabilities capabilities, bool usesLanguageModel)
        : base(id, displayName, WindowsModels.ProviderName, capabilities)
    {
        _usesLanguageModel = usesLanguageModel;
    }

    protected override IReadOnlyList<KeyValuePair<AcquisitionStage, double>> AcquisitionPlan { get; } =
    [
        new(AcquisitionStage.Preparing, 0.05),
        new(AcquisitionStage.DownloadingModel, 0.9),
        new(AcquisitionStage.Loading, 0.05),
    ];

    protected abstract AIFeatureReadyState GetNativeReadyState();

    protected abstract IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync();

    protected override ValueTask<ModelAvailability> GetAvailabilityCoreAsync(CancellationToken cancellationToken) => new(CheckAvailability());

    protected override async Task<ModelAvailability> EnsureReadyCoreAsync(AcquisitionProgressReporter progress, CancellationToken cancellationToken)
    {
        progress.Report(AcquisitionStage.Preparing, 0);
        var availability = CheckAvailability();
        if (availability.Status != ModelAvailabilityStatus.NotReady)
        {
            return availability;
        }

        progress.Report(AcquisitionStage.Preparing, 1);
        AIFeatureReadyResult result;
        try
        {
            // Windows delivers and services the model; the app only waits. Progress may be 0–1 or 0–100.
            result = await EnsureNativeReadyAsync()
                .AsTask(cancellationToken, new SynchronousProgress<double>(p => progress.Report(AcquisitionStage.DownloadingModel, p > 1 ? p / 100 : p)))
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (WindowsAppRequirements.TryMapException(ex) is { } mapped)
            {
                return mapped;
            }

            throw new LocalModelNotReadyException($"Windows could not prepare the model '{Id}': {ex.Message}", ex) { ModelId = Id };
        }

        if (result.Status != AIFeatureReadyResultState.Success)
        {
            throw new LocalModelNotReadyException(
                $"Windows could not prepare the model '{Id}': {result.ErrorDisplayText}",
                result.ExtendedError ?? result.Error)
            {
                ModelId = Id,
            };
        }

        progress.Report(AcquisitionStage.Loading, 1);
        return ModelAvailability.Ready;
    }

    protected override async Task<TClient> CreateClientCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await CreateNativeClientAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not LocalModelException && WindowsAppRequirements.TryMapException(ex) is { } mapped)
        {
            throw new LocalModelNotSupportedException(mapped, Id);
        }
    }

    protected abstract Task<TClient> CreateNativeClientAsync(CancellationToken cancellationToken);

    private ModelAvailability CheckAvailability()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return new ModelAvailability(ModelAvailabilityStatus.NotSupportedOnPlatform, "Windows AI APIs require Windows 11.");
        }

        if (!WindowsAppRequirements.HasPackageIdentity)
        {
            return WindowsAppRequirements.MissingIdentity;
        }

        if (_usesLanguageModel && WindowsAppRequirements.EnsureLimitedAccessFeatureUnlocked() is { } locked)
        {
            return locked;
        }

        try
        {
            return GetNativeReadyState() switch
            {
                AIFeatureReadyState.Ready => ModelAvailability.Ready,
                AIFeatureReadyState.NotReady => ModelAvailability.NotReady,
                AIFeatureReadyState.DisabledByUser => new(ModelAvailabilityStatus.DisabledByUser, "The feature is turned off in Windows Settings (Privacy & security)."),
                AIFeatureReadyState.CapabilityMissing => WindowsAppRequirements.MissingCapability,
                AIFeatureReadyState.NotCompatibleWithSystemHardware => new(ModelAvailabilityStatus.NotSupportedOnDevice, "This device's hardware can't run the model (a Copilot+ PC NPU is required)."),
                AIFeatureReadyState.OSUpdateNeeded => new(ModelAvailabilityStatus.NotSupportedOnPlatform, "A Windows update is required to use this model."),
                _ => new(ModelAvailabilityStatus.NotSupportedOnDevice, "The model isn't supported on this system."),
            };
        }
        catch (Exception ex) when (WindowsAppRequirements.TryMapException(ex) is { } mapped)
        {
            return mapped;
        }
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
