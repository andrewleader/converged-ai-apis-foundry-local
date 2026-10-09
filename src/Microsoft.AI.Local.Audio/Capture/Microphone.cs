namespace Microsoft.AI.Local;

/// <summary>An audio capture device.</summary>
/// <param name="Id">The platform device identifier; pass it to <see cref="MicrophoneOptions.DeviceId"/>.</param>
/// <param name="Name">The human-readable device name.</param>
/// <param name="IsDefault">Whether this is the system's default capture device.</param>
public sealed record MicrophoneDevice(string Id, string Name, bool IsDefault);

/// <summary>Options for <see cref="Microphone.StartAsync"/>.</summary>
public sealed record MicrophoneOptions
{
    /// <summary>
    /// Gets the device to capture from (see <see cref="Microphone.GetDevicesAsync"/>), or <see langword="null"/> for
    /// the system's default capture device.
    /// </summary>
    public string? DeviceId { get; init; }

    /// <summary>
    /// Gets the format of the captured audio. Defaults to <see cref="AudioFormat.Speech"/> (16 kHz, mono, 16-bit),
    /// which speech models consume directly. Must be <see cref="AudioSampleFormat.Pcm16"/>.
    /// </summary>
    public AudioFormat Format { get; init; } = AudioFormat.Speech;
}

/// <summary>
/// Captures audio from a microphone as a <see cref="MicrophoneStream"/>, which any speech-to-text model can transcribe.
/// </summary>
/// <remarks>
/// <para>
/// Capture is separate from recognition: the same microphone stream works with every speech-to-text model, for
/// example <c>SpeechToTextModels.WindowsDefault</c> or <c>SpeechToTextModels.WhisperTiny</c>, and every model also
/// accepts audio from files or a <see cref="PushAudioStream"/>.
/// </para>
/// <code>
/// await using var microphone = await Microphone.StartAsync();
/// using var speech = await SpeechToTextModels.WindowsDefault.CreateClientAsync();
/// await foreach (var update in speech.GetStreamingTextAsync(microphone))
/// {
///     Console.WriteLine(update.Text);
/// }
/// // Elsewhere, for example in a button handler: microphone.Stop();
/// </code>
/// <para>
/// Capture is implemented on Windows (apps built for a Windows target framework, using the Windows audio stack).
/// Packaged apps need the <c>microphone</c> device capability, and users can turn off microphone access in
/// Settings &gt; Privacy &amp; security &gt; Microphone. Elsewhere, <see cref="IsSupported"/> is
/// <see langword="false"/>; supply audio with a <see cref="PushAudioStream"/> instead.
/// </para>
/// </remarks>
public static partial class Microphone
{
    /// <summary>Gets a value indicating whether microphone capture is available in this app on this platform.</summary>
    public static bool IsSupported => IsSupportedCore();

    /// <summary>Lists the audio capture devices.</summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The devices; empty if capture isn't supported here.</returns>
    public static Task<IReadOnlyList<MicrophoneDevice>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
        GetDevicesCoreAsync(cancellationToken);

    /// <summary>Starts capturing from a microphone.</summary>
    /// <param name="options">The device and format; <see langword="null"/> for the default device in <see cref="AudioFormat.Speech"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>A live stream of the captured audio. Call <see cref="MicrophoneStream.Stop"/> or dispose it to stop capturing.</returns>
    /// <exception cref="PlatformNotSupportedException">Microphone capture isn't available on this platform (see <see cref="IsSupported"/>).</exception>
    /// <exception cref="UnauthorizedAccessException">The app isn't allowed to use the microphone.</exception>
    /// <exception cref="InvalidOperationException">No capture device is available, or capture couldn't start.</exception>
    public static Task<MicrophoneStream> StartAsync(MicrophoneOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new MicrophoneOptions();
        if (options.Format.SampleRate == 0 || options.Format.SampleFormat != AudioSampleFormat.Pcm16)
        {
            throw new ArgumentException("MicrophoneOptions.Format must be a 16-bit PCM format, for example AudioFormat.Speech.", nameof(options));
        }

        return StartCoreAsync(options, cancellationToken);
    }

    private static PlatformNotSupportedException NotSupported() => new(
        OperatingSystem.IsWindows()
            ? "Microphone capture needs an app built for a Windows target framework (net8.0-windows10.0.19041.0 or later). Supply audio with a PushAudioStream instead."
            : "Microphone capture isn't available on this platform yet. Supply audio with a PushAudioStream instead.");
}

/// <summary>
/// A live stream of microphone audio in WAV format. Pass it to a speech-to-text client; call <see cref="Stop"/> to end
/// the audio, after which the client finishes transcribing.
/// </summary>
public sealed partial class MicrophoneStream : LiveAudioStream
{
    private int _stopped;

    private MicrophoneStream(AudioFormat format, MicrophoneDevice device)
        : base(format)
    {
        Device = device;
    }

    /// <summary>Gets the device being captured.</summary>
    public MicrophoneDevice Device { get; }

    /// <summary>Stops capturing and ends the stream. Safe to call more than once.</summary>
    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
            // End the audio first, so readers always finish even if releasing the device fails.
            CompleteAudio();
            StopCapture();
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
        }

        base.Dispose(disposing);
    }

    internal void Fail(Exception error)
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
            CompleteAudio(error);
            StopCapture();
        }
    }

    partial void StopCapture();
}
