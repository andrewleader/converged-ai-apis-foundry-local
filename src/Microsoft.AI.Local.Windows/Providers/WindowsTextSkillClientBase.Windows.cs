using Microsoft.Windows.AI.Text;

namespace Microsoft.AI.Local.Windows.Providers;

/// <summary>The base class of the clients that wrap a Phi Silica text skill (summarizer, rewriter, ...).</summary>
/// <remarks>This type is intended for Windows task provider packages.</remarks>
public abstract class WindowsTextSkillClientBase : ILocalAIClient
{
    private readonly object _native;

    /// <summary>Initializes a new instance of the <see cref="WindowsTextSkillClientBase"/> class.</summary>
    /// <param name="model">The language model. The client owns and disposes it.</param>
    /// <param name="handle">The model handle that created the client.</param>
    /// <param name="native">The native skill object, exposed through <see cref="GetService"/>.</param>
    protected WindowsTextSkillClientBase(LanguageModel model, ILocalModel handle, object native)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Handle = handle ?? throw new ArgumentNullException(nameof(handle));
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    /// <summary>Gets the language model.</summary>
    protected LanguageModel Model { get; }

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
            : serviceType.IsInstanceOfType(_native) ? _native
            : serviceType.IsInstanceOfType(Model) ? Model
            : serviceType.IsInstanceOfType(MetadataObject) ? MetadataObject
            : serviceType.IsInstanceOfType(Handle) ? Handle
            : null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Model.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Throws for a failed status, then stamps the model ID and raw representation on <paramref name="result"/>.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="status">The native status.</param>
    /// <param name="error">The native extended error.</param>
    /// <param name="raw">The native result.</param>
    /// <returns><paramref name="result"/>.</returns>
    protected TResult Complete<TResult>(TResult result, LanguageModelResponseStatus status, Exception? error, object raw)
        where TResult : LocalAIResult
    {
        ArgumentNullException.ThrowIfNull(result);
        WindowsAIInterop.ThrowIfFailed(status, error, Handle.Id);
        result.ModelId = Handle.Id;
        result.RawRepresentation = raw;
        return result;
    }
}
