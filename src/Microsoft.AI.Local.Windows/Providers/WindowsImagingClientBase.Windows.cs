using Microsoft.Graphics.Imaging;

namespace Microsoft.AI.Local.Windows.Providers;

/// <summary>The base class of the clients that wrap a Windows imaging API (OCR, super-resolution, ...).</summary>
/// <remarks>This type is intended for Windows task provider packages.</remarks>
public abstract class WindowsImagingClientBase : ILocalAIClient
{
    private readonly IDisposable? _native;

    /// <summary>Initializes a new instance of the <see cref="WindowsImagingClientBase"/> class.</summary>
    /// <param name="handle">The model handle that created the client.</param>
    /// <param name="native">The native object, exposed through <see cref="GetService"/> and disposed with the client.</param>
    protected WindowsImagingClientBase(ILocalModel handle, IDisposable? native)
    {
        Handle = handle ?? throw new ArgumentNullException(nameof(handle));
        _native = native;
    }

    /// <summary>Gets the model handle that created the client.</summary>
    protected ILocalModel Handle { get; }

    /// <summary>Gets the client's metadata object, exposed through <see cref="GetService"/>.</summary>
    protected abstract object MetadataObject { get; }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }

        return serviceType.IsInstanceOfType(this) ? this
            : _native is not null && serviceType.IsInstanceOfType(_native) ? _native
            : serviceType.IsInstanceOfType(MetadataObject) ? MetadataObject
            : serviceType.IsInstanceOfType(Handle) ? Handle
            : null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _native?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Runs <paramref name="action"/> with an <see cref="ImageBuffer"/> for <paramref name="image"/>, disposing it if it was created.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="image">The image.</param>
    /// <param name="action">The operation.</param>
    /// <returns>The result of <paramref name="action"/>.</returns>
    protected static async Task<T> WithImageBufferAsync<T>(ImageFrame image, Func<ImageBuffer, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(action);
        var buffer = WindowsImageBuffers.ToImageBuffer(image, out var created);
        try
        {
            return await action(buffer).ConfigureAwait(false);
        }
        finally
        {
            if (created)
            {
                buffer.Dispose();
            }
        }
    }
}
