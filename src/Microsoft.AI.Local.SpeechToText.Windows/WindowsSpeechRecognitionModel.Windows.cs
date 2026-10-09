using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.AI.Local.Providers;
using Microsoft.AI.Local.Windows.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Speech;
using Windows.Foundation;

namespace Microsoft.AI.Local.Windows;

internal static partial class WindowsModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => new WindowsSpeechRecognitionModel(descriptor);
}

/// <summary>The Windows speech recognition model (<c>Microsoft.Windows.AI.Speech</c>, experimental Windows App SDK).</summary>
internal sealed class WindowsSpeechRecognitionModel(LocalModelDescriptor descriptor)
    : WindowsModelBase<ISpeechToTextClient>(descriptor, usesLanguageModel: false), ISpeechToTextModel
{
    private const int ClassNotRegistered = unchecked((int)0x80040154);
    private const int FacilityWeb = 0x375;

    private static readonly ModelAvailability ExperimentalSdkMissing = new(
        ModelAvailabilityStatus.MissingAppRequirement,
        "Windows speech recognition is part of the experimental Windows App SDK, and this app runs a Windows App SDK without it. " +
        "Reference an experimental Microsoft.WindowsAppSDK.AI package (2.5.4-experimental or later) and deploy the matching " +
        "experimental Windows App SDK runtime (or build self-contained). Build warning MSAILOCAL104 detects this at build time.");

    protected override ModelAvailability? CheckModelRequirements()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
        {
            return new ModelAvailability(ModelAvailabilityStatus.NotSupportedOnPlatform, "Windows speech recognition requires Windows 11, version 24H2 (build 26100) or later.");
        }

        return SpeechProjection.IsAvailable ? null : ExperimentalSdkMissing;
    }

    protected override ModelAvailability? TryMapException(Exception exception) => exception switch
    {
        // The projection is present but the runtime has no speech classes: a stable Windows App SDK runtime.
        COMException { HResult: ClassNotRegistered } or TypeLoadException or FileNotFoundException => ExperimentalSdkMissing,

        // WEB_E_JSON_* (for example 0x83750009, "JSON value not found"): the speech API couldn't resolve a model package
        // for this device. Its EnsureReadyAsync doesn't complete in that state, so report it instead of trying.
        COMException com when ((com.HResult >> 16) & 0x1FFF) == FacilityWeb => ModelPackageUnavailable(com),
        _ => base.TryMapException(exception),
    };

    private static ModelAvailability ModelPackageUnavailable(COMException exception) => new(
        ModelAvailabilityStatus.NotSupportedOnDevice,
        $"The experimental Windows speech API couldn't find a speech recognition model for this device ({Describe(exception)}). " +
        "Windows delivers the model as a separate package for the device's NPU or CPU; this error means none is available for this " +
        "device, locale or Windows build, so the app can't fix it. Use another speech model on this device. " +
        $"Diagnostics: {Diagnose()}.");

    private static string Describe(Exception exception)
    {
        var message = exception.Message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        return $"0x{exception.HResult:X8}{(string.IsNullOrEmpty(message) ? string.Empty : ": " + message)}";
    }

    // Asks the speech API about each device it supports, to show which part of model resolution fails.
    private static string Diagnose()
    {
        string locale;
        try
        {
            locale = SpeechRecognitionModelFactoryOptions.DefaultLocale;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return $"locale unavailable ({Describe(ex)})";
        }

        var results = new List<string> { $"locale {locale}" };
        foreach (var device in (ReadOnlySpan<AIComputeDevice>)[AIComputeDevice.NPU, AIComputeDevice.CPU])
        {
            try
            {
                var factory = new SpeechRecognitionModelFactory(new SpeechRecognitionModelFactoryOptions(locale, [device]));
                results.Add($"{device} {factory.GetReadyState()}");
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException)
            {
                results.Add($"{device} error {Describe(ex)}");
            }
        }

        results.Add($"Windows {Environment.OSVersion.Version}, {RuntimeInformation.ProcessArchitecture}");
        return string.Join("; ", results);
    }

    protected override AIFeatureReadyState GetNativeReadyState() => SpeechRecognitionModelFactory.Default.GetReadyState();

    protected override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() =>
        AsyncInfo.Run<AIFeatureReadyResult, double>((cancellationToken, progress) =>
            SpeechRecognitionModelFactory.Default.EnsureReadyAsync()
                .AsTask(cancellationToken, new SyncProgress<SpeechRecognitionModelProgress>(p => progress.Report(p.Progress))));

    protected override async Task<ISpeechToTextClient> CreateNativeClientAsync(CancellationToken cancellationToken)
    {
        var model = await SpeechRecognitionModelFactory.Default.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false);
        return new WindowsSpeechToTextClient(model, this);
    }

    /// <summary>Reports progress on the calling thread, so stage progress stays ordered.</summary>
    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    /// <summary>Detects whether the speech projection assembly (experimental Windows App SDK only) can be loaded.</summary>
    private static class SpeechProjection
    {
        public static bool IsAvailable { get; } = Probe();

        private static bool Probe()
        {
            try
            {
                Touch();
                return true;
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or TypeLoadException)
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Touch() => GC.KeepAlive(typeof(SpeechRecognitionModelFactory).TypeHandle);
    }
}
