namespace Microsoft.AI.Local.Foundry.Runtime;

/// <summary>A seam over the Foundry Local audio API. FoundryLocalSpeechEngine implements it.</summary>
internal interface IFoundrySpeechEngine
{
    Task<FoundrySpeechResult> TranscribeAsync(FoundryAudio audio, CancellationToken cancellationToken);

    IAsyncEnumerable<FoundrySpeechSegment> StreamAsync(FoundryAudio audio, CancellationToken cancellationToken);

    /// <summary>
    /// Starts a live transcription that accepts 16-bit PCM in <paramref name="format"/> as it is captured. A model
    /// that can't transcribe live fails when its results are read.
    /// </summary>
    Task<IFoundryLiveTranscription> StartLiveAsync(AudioFormat format, string? language, CancellationToken cancellationToken);
}

/// <summary>A live transcription session: audio goes in while results come out.</summary>
internal interface IFoundryLiveTranscription : IAsyncDisposable
{
    /// <summary>Appends 16-bit PCM audio in the session's format.</summary>
    ValueTask AppendAsync(ReadOnlyMemory<byte> pcm, CancellationToken cancellationToken);

    /// <summary>Gets the results; the sequence ends after <see cref="StopAsync"/> has flushed the last result.</summary>
    IAsyncEnumerable<FoundrySpeechSegment> GetResultsAsync(CancellationToken cancellationToken);

    /// <summary>Ends the audio: the session transcribes what's left, then ends the results.</summary>
    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>Encoded audio (<paramref name="Format"/> is e.g. <c>wav</c>, <c>mp3</c>, <c>flac</c>).</summary>
internal sealed record FoundryAudio(string Format, ReadOnlyMemory<byte> Data);

internal sealed record FoundrySpeechSegment(string Text, TimeSpan? Start, TimeSpan? End, bool IsFinal, string? Language);

internal sealed record FoundrySpeechResult(string Text, string? Language, TimeSpan? Duration, IReadOnlyList<FoundrySpeechSegment> Segments, object? RawRepresentation = null);
