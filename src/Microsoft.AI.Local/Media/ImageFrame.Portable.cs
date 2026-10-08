namespace Microsoft.AI.Local;

public sealed partial class ImageFrame
{
    // No platform codec on portable target frameworks; PNG is handled by the built-in codec.
    private static Task<ImageFrame?> DecodeWithPlatformCodecAsync(ReadOnlyMemory<byte> data, string mediaType, CancellationToken cancellationToken)
    {
        _ = data;
        _ = mediaType;
        _ = cancellationToken;
        return Task.FromResult<ImageFrame?>(null);
    }
}
