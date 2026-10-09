using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Windows.AI.Speech;

namespace Microsoft.AI.Local.Windows;

/// <summary>
/// An <see cref="ISpeechToTextClient"/> over the Windows speech recognition model. Accepts any audio stream: WAV
/// files, headerless PCM (with <see cref="SpeechToTextOptions.SpeechSampleRate"/>), and live streams such as
/// <see cref="MicrophoneStream"/> and <see cref="PushAudioStream"/>. Audio is converted to the model's 16 kHz mono
/// 16-bit PCM and pushed to it, so every audio source goes through the same path.
/// </summary>
internal sealed class WindowsSpeechToTextClient : ISpeechToTextClient
{
    // How much audio is pushed per call, and the silence pushed after the input ends so the last phrase is finalized.
    private const int ChunkSamples = 1600;
    private const int TrailingSilenceSamples = 16000;

    private readonly SpeechRecognitionModel _model;
    private readonly ILocalModel _handle;
    private readonly SpeechToTextClientMetadata _metadata;

    public WindowsSpeechToTextClient(SpeechRecognitionModel model, ILocalModel handle)
    {
        _model = model;
        _handle = handle;
        _metadata = new SpeechToTextClientMetadata(WindowsAIProvider.ProviderName, providerUri: null, defaultModelId: handle.Id);
    }

    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream, SpeechToTextOptions? options = null, CancellationToken cancellationToken = default)
    {
        var input = await OpenAsync(audioSpeechStream, options, cancellationToken).ConfigureAwait(false);
        var reader = input.OpenPcm(AudioFormat.Speech);
        var samples = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        using var batch = new BatchRecognition(_model);
        var text = await batch.Recognize(MemoryMarshal.Cast<short, ushort>(samples).ToArray()).AsTask(cancellationToken).ConfigureAwait(false);
        return new SpeechToTextResponse(text ?? string.Empty)
        {
            ResponseId = NewId(),
            ModelId = _handle.Id,
            StartTime = TimeSpan.Zero,
            EndTime = reader.Position,
        };
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var input = await OpenAsync(audioSpeechStream, options, cancellationToken).ConfigureAwait(false);
        var reader = input.OpenPcm(AudioFormat.Speech);
        var responseId = NewId();
        var updates = Channel.CreateUnbounded<SpeechToTextResponseUpdate>(new UnboundedChannelOptions { SingleReader = true });

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var audio = new SpeechAudioProvider();
        using var recognition = new StreamingRecognition(AudioConfiguration.ForProvider(audio), _model);

        // Events arrive on Windows threads; the channel hands them to the consumer in order.
        recognition.Recognizing += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Text))
            {
                updates.Writer.TryWrite(new SpeechToTextResponseUpdate(e.Text)
                {
                    Kind = SpeechToTextResponseUpdateKind.TextUpdating,
                    ResponseId = responseId,
                    ModelId = _handle.Id,
                    RawRepresentation = e,
                });
            }
        };
        recognition.Recognized += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Text))
            {
                // The native offsets are in milliseconds from the start of the audio.
                var start = TimeSpan.FromMilliseconds(e.Offset);
                updates.Writer.TryWrite(new SpeechToTextResponseUpdate(e.Text)
                {
                    Kind = SpeechToTextResponseUpdateKind.TextUpdated,
                    ResponseId = responseId,
                    ModelId = _handle.Id,
                    StartTime = start,
                    EndTime = start + TimeSpan.FromMilliseconds(e.Duration),
                    RawRepresentation = e,
                });
            }
        };

        await recognition.StartContinuousRecognitionAsync().AsTask(cancellationToken).ConfigureAwait(false);
        var pump = PumpAsync(reader, audio, recognition, updates.Writer, stop.Token);
        try
        {
            await foreach (var update in updates.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }

            await pump.ConfigureAwait(false);
        }
        finally
        {
            // A pump failure already reached the consumer through the channel.
            if (pump.IsFaulted)
            {
                _ = pump.Exception;
            }

            // The consumer stopped early (break, cancellation or error): stop pushing audio and recognizing.
            if (!pump.IsCompleted)
            {
                await stop.CancelAsync().ConfigureAwait(false);
                try
                {
                    await pump.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }

        return serviceType == typeof(SpeechToTextClientMetadata) ? _metadata
            : serviceType.IsInstanceOfType(this) ? this
            : serviceType.IsInstanceOfType(_model) ? _model
            : serviceType.IsInstanceOfType(_handle) ? _handle
            : null;
    }

    public void Dispose() => _model.Dispose();

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static async Task<AudioInput> OpenAsync(Stream stream, SpeechToTextOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ReportUnsupportedOptions(options);
        AudioFormat? raw = options?.SpeechSampleRate is { } rate and > 0 ? new AudioFormat(rate, 1, AudioSampleFormat.Pcm16) : null;
        return await AudioInput.OpenAsync(stream, raw, cancellationToken).ConfigureAwait(false);
    }

    private static void ReportUnsupportedOptions(SpeechToTextOptions? options)
    {
        // The language is chosen when the model is created (the user's Windows display language by default).
        if (options?.SpeechLanguage is not null)
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(WindowsAIProvider.ProviderName, "SpeechToTextOptions.SpeechLanguage");
        }

        if (options?.TextLanguage is not null)
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(WindowsAIProvider.ProviderName, "SpeechToTextOptions.TextLanguage");
        }
    }

    // Pushes the audio to the recognizer as it arrives, then flushes the last phrase and stops recognition.
    private static async Task PumpAsync(
        PcmAudioReader reader,
        SpeechAudioProvider audio,
        StreamingRecognition recognition,
        ChannelWriter<SpeechToTextResponseUpdate> updates,
        CancellationToken cancellationToken)
    {
        Exception? error = null;
        try
        {
            var buffer = new short[ChunkSamples];
            int read;
            while ((read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                audio.PushData(MemoryMarshal.Cast<short, ushort>(buffer.AsSpan(0, read)).ToArray());
            }

            audio.PushData(new ushort[TrailingSilenceSamples]);
            await recognition.StopContinuousRecognitionAsync().AsTask(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            error = ex;
            try
            {
                await recognition.StopContinuousRecognitionAsync().AsTask(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception stopError) when (stopError is COMException or InvalidOperationException or ObjectDisposedException)
            {
            }

            throw;
        }
        finally
        {
            updates.TryComplete(error);
        }
    }
}
