using Microsoft.AI.Local.Providers;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using Windows.Foundation;

namespace Microsoft.AI.Local.Windows.Providers;

/// <summary>The base class of the Windows models that run on Phi Silica (<see cref="LanguageModel"/>).</summary>
/// <remarks>This type is intended for Windows task provider packages.</remarks>
/// <typeparam name="TClient">The inference client contract.</typeparam>
public abstract class WindowsLanguageModelBase<TClient> : WindowsModelBase<TClient>
    where TClient : class
{
    /// <summary>Initializes a new instance of the <see cref="WindowsLanguageModelBase{TClient}"/> class.</summary>
    /// <param name="descriptor">The model descriptor from the Windows catalog manifest.</param>
    protected WindowsLanguageModelBase(LocalModelDescriptor descriptor)
        : base(descriptor, usesLanguageModel: true)
    {
    }

    /// <inheritdoc/>
    protected sealed override AIFeatureReadyState GetNativeReadyState() => LanguageModel.GetReadyState();

    /// <inheritdoc/>
    protected sealed override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() => LanguageModel.EnsureReadyAsync();

    /// <inheritdoc/>
    protected sealed override async Task<TClient> CreateNativeClientAsync(CancellationToken cancellationToken)
    {
        var model = await LanguageModel.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false);
        try
        {
            return Wrap(model);
        }
        catch
        {
            model.Dispose();
            throw;
        }
    }

    /// <summary>Wraps a new <see cref="LanguageModel"/> in the client. The client owns the model.</summary>
    /// <param name="model">The language model.</param>
    /// <returns>The client.</returns>
    protected abstract TClient Wrap(LanguageModel model);
}
