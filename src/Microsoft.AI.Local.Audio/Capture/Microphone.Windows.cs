using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.AI.Local.Providers;
using Windows.Devices.Enumeration;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Media.Render;

namespace Microsoft.AI.Local;

// Windows: capture with an AudioGraph (device input node → frame output node). The graph runs in the requested sample
// rate and channel count, so Windows resamples and mixes the device's audio; every quantum (~10 ms) the float samples
// are converted to 16-bit PCM and appended to the stream.
//
// AudioGraph rules: frames are read only in QuantumStarted (the audio thread), and the graph must never be stopped
// or disposed on the audio thread ("It is not allowed to invoke this method from the current thread").
public static partial class Microphone
{
    private static bool IsSupportedCore() => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763);

    private static async Task<IReadOnlyList<MicrophoneDevice>> GetDevicesCoreAsync(CancellationToken cancellationToken)
    {
        if (!IsSupportedCore())
        {
            return [];
        }

        var defaultId = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default);
        var devices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture).AsTask(cancellationToken).ConfigureAwait(false);
        return [.. devices.Where(d => d.IsEnabled).Select(d => new MicrophoneDevice(d.Id, d.Name, string.Equals(d.Id, defaultId, StringComparison.OrdinalIgnoreCase)))];
    }

    private static async Task<MicrophoneStream> StartCoreAsync(MicrophoneOptions options, CancellationToken cancellationToken)
    {
        if (!IsSupportedCore())
        {
            throw NotSupported();
        }

        var deviceId = options.DeviceId ?? MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default);
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new InvalidOperationException("No microphone is available. Connect a microphone or enable one in Settings > System > Sound.");
        }

        var device = await DeviceInformation.CreateFromIdAsync(deviceId).AsTask(cancellationToken).ConfigureAwait(false);

        // Ask for a graph in the output's rate and channels; if the system can't, use its default format and convert.
        var settings = new AudioGraphSettings(AudioRenderCategory.Speech);
        var encoding = AudioEncodingProperties.CreatePcm((uint)options.Format.SampleRate, (uint)options.Format.Channels, 32);
        encoding.Subtype = MediaEncodingSubtypes.Float;
        settings.EncodingProperties = encoding;
        var graphResult = await AudioGraph.CreateAsync(settings).AsTask(cancellationToken).ConfigureAwait(false);
        if (graphResult.Status != AudioGraphCreationStatus.Success)
        {
            graphResult = await AudioGraph.CreateAsync(new AudioGraphSettings(AudioRenderCategory.Speech)).AsTask(cancellationToken).ConfigureAwait(false);
        }

        if (graphResult.Status != AudioGraphCreationStatus.Success)
        {
            throw new InvalidOperationException($"Audio capture couldn't start ({graphResult.Status}).", graphResult.ExtendedError);
        }

        var graph = graphResult.Graph;
        try
        {
            var inputResult = await graph.CreateDeviceInputNodeAsync(MediaCategory.Speech, null, device)
                .AsTask(cancellationToken).ConfigureAwait(false);
            switch (inputResult.Status)
            {
                case AudioDeviceNodeCreationStatus.Success:
                    break;
                case AudioDeviceNodeCreationStatus.AccessDenied:
                    throw new UnauthorizedAccessException(
                        "Microphone access was denied. Packaged apps need <DeviceCapability Name=\"microphone\"/> in Package.appxmanifest, " +
                        "and microphone access must be on in Settings > Privacy & security > Microphone.",
                        inputResult.ExtendedError);
                default:
                    throw new InvalidOperationException($"The microphone '{device.Name}' couldn't be opened ({inputResult.Status}).", inputResult.ExtendedError);
            }

            var output = graph.CreateFrameOutputNode();
            inputResult.DeviceInputNode.AddOutgoingConnection(output);
            var properties = graph.EncodingProperties;
            var sourceFormat = new AudioFormat((int)properties.SampleRate, (int)properties.ChannelCount, AudioSampleFormat.Float32);
            var defaultId = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default);
            var info = new MicrophoneDevice(device.Id, device.Name, string.Equals(device.Id, defaultId, StringComparison.OrdinalIgnoreCase));
            return MicrophoneStream.Start(options.Format, info, graph, inputResult.DeviceInputNode, output, sourceFormat);
        }
        catch
        {
            graph.Dispose();
            throw;
        }
    }
}

public sealed partial class MicrophoneStream
{
    [ThreadStatic]
    private static bool onAudioThread;

    private readonly List<short> _converted = [];
    private AudioGraph? _graph;
    private AudioDeviceInputNode? _input;
    private AudioFrameOutputNode? _output;
    private AudioConverter? _converter;

    internal static MicrophoneStream Start(
        AudioFormat format,
        MicrophoneDevice device,
        AudioGraph graph,
        AudioDeviceInputNode input,
        AudioFrameOutputNode output,
        AudioFormat sourceFormat)
    {
        var stream = new MicrophoneStream(format, device)
        {
            _graph = graph,
            _input = input,
            _output = output,
            _converter = new AudioConverter(sourceFormat, format),
        };

        graph.QuantumStarted += stream.OnQuantumStarted;
        graph.UnrecoverableErrorOccurred += stream.OnUnrecoverableError;
        graph.Start();
        return stream;
    }

    partial void StopCapture()
    {
        // A failure while reading a frame is reported on the audio thread, where the graph can't be stopped.
        if (onAudioThread)
        {
            _ = Task.Run(ReleaseGraph);
        }
        else
        {
            ReleaseGraph();
        }
    }

    private void ReleaseGraph()
    {
        var graph = Interlocked.Exchange(ref _graph, null);
        if (graph is null)
        {
            return;
        }

        // Release the device even if the graph is already in a failed state.
        Try(() => graph.QuantumStarted -= OnQuantumStarted);
        Try(() => graph.UnrecoverableErrorOccurred -= OnUnrecoverableError);
        Try(graph.Stop);
        Try(() => _output?.Dispose());
        Try(() => _input?.Dispose());
        Try(graph.Dispose);
    }

    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or ObjectDisposedException or UnauthorizedAccessException)
        {
        }
    }

    private void OnQuantumStarted(AudioGraph sender, object args)
    {
        if (IsCompleted || _output is not { } output || _converter is not { } converter)
        {
            return;
        }

        onAudioThread = true;
        try
        {
            using var frame = output.GetFrame();
            using var buffer = frame.LockBuffer(AudioBufferAccessMode.Read);
            var length = (int)buffer.Length;
            if (length == 0)
            {
                return;
            }

            // The copy's Length starts at 0; only its capacity matches the frame.
            var copy = Windows.Storage.Streams.Buffer.CreateCopyFromMemoryBuffer(buffer);
            copy.Length = (uint)length;
            var bytes = copy.ToArray(0, length);
            _converted.Clear();
            converter.Convert(bytes, _converted);
            WriteAudio(System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_converted)));
        }
        catch (Exception ex)
        {
            Fail(new InvalidOperationException($"Microphone capture failed: {ex.Message}", ex));
        }
        finally
        {
            onAudioThread = false;
        }
    }

    // Raised on an audio thread; StopCapture moves the teardown off it.
    private void OnUnrecoverableError(AudioGraph sender, AudioGraphUnrecoverableErrorOccurredEventArgs args)
    {
        onAudioThread = true;
        try
        {
            Fail(new InvalidOperationException($"Microphone capture stopped unexpectedly ({args.Error})."));
        }
        finally
        {
            onAudioThread = false;
        }
    }
}
