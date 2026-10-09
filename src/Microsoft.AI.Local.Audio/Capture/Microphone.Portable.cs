namespace Microsoft.AI.Local;

// Platform-neutral build: there is no capture implementation yet.
public static partial class Microphone
{
    private static bool IsSupportedCore() => false;

    private static Task<IReadOnlyList<MicrophoneDevice>> GetDevicesCoreAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MicrophoneDevice>>([]);

    private static Task<MicrophoneStream> StartCoreAsync(MicrophoneOptions options, CancellationToken cancellationToken) =>
        Task.FromException<MicrophoneStream>(NotSupported());
}
