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
        _ => base.TryMapException(exception),
    };

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
