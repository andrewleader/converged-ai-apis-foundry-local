using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.AI.Local.Providers;

/// <summary>
/// A base class for provider model handles that implements the cross-cutting behavior of
/// <see cref="ILocalModel"/>: coalesced, idempotent acquisition; reference-counted cancellation;
/// normalized, monotonic progress; logging and telemetry.
/// </summary>
/// <remarks>This type is intended for provider authors. App code uses the <see cref="ILocalModel"/> interfaces.</remarks>
public abstract class LocalModelBase : ILocalModel
{
    private readonly object _gate = new();
    private Acquisition? _current;

    /// <summary>Initializes a new instance of the <see cref="LocalModelBase"/> class.</summary>
    /// <param name="id">The stable model identifier, for example <c>foundry/phi-4-mini</c>.</param>
    /// <param name="displayName">A human-readable name.</param>
    /// <param name="providerName">The provider name, for example <c>Foundry</c>.</param>
    /// <param name="capabilities">The capabilities of the model.</param>
    protected LocalModelBase(string id, string displayName, string providerName, LocalModelCapabilities? capabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        Id = id;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        ProviderName = providerName;
        Capabilities = capabilities ?? LocalModelCapabilities.None;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string DisplayName { get; }

    /// <inheritdoc/>
    public string ProviderName { get; }

    /// <inheritdoc/>
    public virtual LocalModelCapabilities Capabilities { get; }

    /// <summary>
    /// Gets the relative weights of the acquisition stages, used to compute
    /// <see cref="ModelAcquisitionProgress.OverallFraction"/>. Stages that are not listed have no weight.
    /// </summary>
    protected virtual IReadOnlyList<KeyValuePair<AcquisitionStage, double>> AcquisitionPlan { get; } =
    [
        new(AcquisitionStage.Preparing, 0.05),
        new(AcquisitionStage.DownloadingRuntime, 0.15),
        new(AcquisitionStage.DownloadingModel, 0.7),
        new(AcquisitionStage.Loading, 0.1),
    ];

    /// <summary>Gets the logger used by the handle.</summary>
    protected ILogger Logger => field ??= LocalAIOptions.Default.LoggerFactory.CreateLogger(GetType());

    /// <inheritdoc/>
    public ValueTask<ModelAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GetAvailabilityCoreAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ModelAvailability> EnsureReadyAsync(
        IProgress<ModelAcquisitionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Acquisition acquisition;
        lock (_gate)
        {
            if (_current is null || _current.Task.IsCompleted || _current.Cancellation.IsCancellationRequested)
            {
                _current = new Acquisition(AcquisitionPlan);
                _current.Task = RunAcquisitionAsync(_current);
            }

            acquisition = _current;
            acquisition.Waiters++;
        }

        acquisition.Progress.Subscribe(progress);
        try
        {
            return await acquisition.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            acquisition.Progress.Unsubscribe(progress);
            lock (_gate)
            {
                // Only callers that gave up (cancelled) leave before completion; the last one cancels the shared work.
                if (--acquisition.Waiters == 0 && !acquisition.Task.IsCompleted)
                {
                    Log.CancellingAcquisition(Logger, Id);
                    acquisition.Cancellation.Cancel();
                }
            }
        }
    }

    /// <summary>Returns the current availability of the model. Must be cheap and must not download anything.</summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The availability.</returns>
    protected abstract ValueTask<ModelAvailability> GetAvailabilityCoreAsync(CancellationToken cancellationToken);

    /// <summary>Acquires the model. Called at most once at a time per handle.</summary>
    /// <param name="progress">Receives raw per-stage progress, which the base class normalizes.</param>
    /// <param name="cancellationToken">Cancelled when every caller waiting for the acquisition has cancelled.</param>
    /// <returns>The availability after acquisition.</returns>
    protected abstract Task<ModelAvailability> EnsureReadyCoreAsync(AcquisitionProgressReporter progress, CancellationToken cancellationToken);

    /// <summary>Throws the exception that corresponds to a non-ready <paramref name="availability"/>.</summary>
    /// <param name="availability">The availability.</param>
    /// <exception cref="LocalModelNotSupportedException">The model is not supported here.</exception>
    /// <exception cref="LocalModelNotReadyException">The model is not ready.</exception>
    protected void ThrowIfNotReady(ModelAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(availability);
        switch (availability.Status)
        {
            case ModelAvailabilityStatus.Ready:
                return;
            case ModelAvailabilityStatus.NotReady:
                throw new LocalModelNotReadyException(
                    availability.Reason is null ? $"The model '{Id}' is not ready." : $"The model '{Id}' is not ready: {availability.Reason}")
                {
                    ModelId = Id,
                };
            default:
                throw new LocalModelNotSupportedException(availability, Id);
        }
    }

    private async Task<ModelAvailability> RunAcquisitionAsync(Acquisition acquisition)
    {
        // Yield so that the caller registers as a waiter before any provider work starts.
        await Task.Yield();

        using var activity = LocalAIDiagnostics.ActivitySource.StartActivity($"ensure_ready {Id}");
        activity?.SetTag(TelemetryTags.ModelId, Id);
        activity?.SetTag(TelemetryTags.Provider, ProviderName);
        var started = Stopwatch.GetTimestamp();
        Log.AcquisitionStarting(Logger, Id);

        string outcome = "error";
        try
        {
            var result = await EnsureReadyCoreAsync(acquisition.Reporter, acquisition.Cancellation.Token).ConfigureAwait(false);
            outcome = result.Status.ToString();
            if (result.Status == ModelAvailabilityStatus.Ready)
            {
                acquisition.Reporter.Complete();
            }

            Log.AcquisitionCompleted(Logger, Id, result.Status, result.Reason);
            return result;
        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            throw;
        }
        catch (Exception ex)
        {
            Log.AcquisitionFailed(Logger, Id, ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().FullName);
            throw;
        }
        finally
        {
            activity?.SetTag(TelemetryTags.Outcome, outcome);
            LocalAIDiagnostics.AcquisitionDuration.Record(
                Stopwatch.GetElapsedTime(started).TotalSeconds,
                new(TelemetryTags.ModelId, Id),
                new(TelemetryTags.Provider, ProviderName),
                new(TelemetryTags.Outcome, outcome));
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Id;

    private sealed class Acquisition
    {
        public Acquisition(IReadOnlyList<KeyValuePair<AcquisitionStage, double>> plan)
        {
            Reporter = new AcquisitionProgressReporter(Progress, plan);
        }

        public ProgressBroadcaster Progress { get; } = new();

        public AcquisitionProgressReporter Reporter { get; }

        public CancellationTokenSource Cancellation { get; } = new();

        public Task<ModelAvailability> Task { get; set; } = null!;

        public int Waiters { get; set; }
    }

    private sealed class ProgressBroadcaster : IProgress<ModelAcquisitionProgress>
    {
        private readonly object _gate = new();
        private IProgress<ModelAcquisitionProgress>[] _subscribers = [];
        private ModelAcquisitionProgress? _last;

        public void Subscribe(IProgress<ModelAcquisitionProgress>? progress)
        {
            if (progress is null)
            {
                return;
            }

            ModelAcquisitionProgress? last;
            lock (_gate)
            {
                _subscribers = [.. _subscribers, progress];
                last = _last;
            }

            // Late joiners immediately see where the shared acquisition is.
            if (last is { } value)
            {
                progress.Report(value);
            }
        }

        public void Unsubscribe(IProgress<ModelAcquisitionProgress>? progress)
        {
            if (progress is null)
            {
                return;
            }

            lock (_gate)
            {
                _subscribers = [.. _subscribers.Where(p => !ReferenceEquals(p, progress))];
            }
        }

        public void Report(ModelAcquisitionProgress value)
        {
            IProgress<ModelAcquisitionProgress>[] subscribers;
            lock (_gate)
            {
                _last = value;
                subscribers = _subscribers;
            }

            foreach (var subscriber in subscribers)
            {
                subscriber.Report(value);
            }
        }
    }
}

/// <summary>
/// A base class for provider model handles that create a task-specific client.
/// </summary>
/// <typeparam name="TClient">The inference client contract.</typeparam>
public abstract class LocalModelBase<TClient> : LocalModelBase, ILocalModel<TClient>
    where TClient : class
{
    /// <summary>Initializes a new instance of the <see cref="LocalModelBase{TClient}"/> class.</summary>
    /// <param name="id">The stable model identifier, for example <c>foundry/phi-4-mini</c>.</param>
    /// <param name="displayName">A human-readable name.</param>
    /// <param name="providerName">The provider name, for example <c>Foundry</c>.</param>
    /// <param name="capabilities">The capabilities of the model.</param>
    protected LocalModelBase(string id, string displayName, string providerName, LocalModelCapabilities? capabilities = null)
        : base(id, displayName, providerName, capabilities)
    {
    }

    /// <inheritdoc/>
    public async Task<TClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        var availability = await EnsureReadyAsync(progress: null, cancellationToken).ConfigureAwait(false);
        ThrowIfNotReady(availability);
        return await CreateClientCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates a client for a ready model.</summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>A new client owned by the caller.</returns>
    protected abstract Task<TClient> CreateClientCoreAsync(CancellationToken cancellationToken);
}

internal static class TelemetryTags
{
    public const string ModelId = "microsoft.ai.local.model.id";
    public const string Provider = "microsoft.ai.local.provider";
    public const string Outcome = "microsoft.ai.local.outcome";
}

internal static partial class Log
{
    [LoggerMessage(1, LogLevel.Information, "Acquiring local model '{ModelId}'.")]
    public static partial void AcquisitionStarting(ILogger logger, string modelId);

    [LoggerMessage(2, LogLevel.Information, "Acquisition of local model '{ModelId}' completed with status {Status}. {Reason}")]
    public static partial void AcquisitionCompleted(ILogger logger, string modelId, ModelAvailabilityStatus status, string? reason);

    [LoggerMessage(3, LogLevel.Warning, "Acquisition of local model '{ModelId}' failed.")]
    public static partial void AcquisitionFailed(ILogger logger, string modelId, Exception exception);

    [LoggerMessage(4, LogLevel.Debug, "All callers cancelled; cancelling acquisition of local model '{ModelId}'.")]
    public static partial void CancellingAcquisition(ILogger logger, string modelId);

    [LoggerMessage(5, LogLevel.Debug, "{Provider} ignores the unsupported option '{OptionName}'. Set LocalAIOptions.Default.ThrowOnUnsupportedOptions to throw instead.")]
    public static partial void UnsupportedOptionIgnored(ILogger logger, string provider, string optionName);
}
