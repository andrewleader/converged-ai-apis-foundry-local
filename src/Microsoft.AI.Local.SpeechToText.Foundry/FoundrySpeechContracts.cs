namespace Microsoft.AI.Local.Foundry.Runtime;

/// <summary>A seam over the Foundry Local audio API. FoundryLocalSpeechEngine implements it.</summary>
internal interface IFoundrySpeechEngine
{
    Task<FoundrySpeechResult> TranscribeAsync(FoundryAudio audio, CancellationToken cancellationToken);

    IAsyncEnumerable<FoundrySpeechSegment> StreamAsync(FoundryAudio audio, CancellationToken cancellationToken);
}

/// <summary>Encoded audio (<paramref name="Format"/> is e.g. <c>wav</c>, <c>mp3</c>, <c>flac</c>).</summary>
internal sealed record FoundryAudio(string Format, ReadOnlyMemory<byte> Data);

internal sealed record FoundrySpeechSegment(string Text, TimeSpan? Start, TimeSpan? End, bool IsFinal, string? Language);

internal sealed record FoundrySpeechResult(string Text, string? Language, TimeSpan? Duration, IReadOnlyList<FoundrySpeechSegment> Segments, object? RawRepresentation = null);
