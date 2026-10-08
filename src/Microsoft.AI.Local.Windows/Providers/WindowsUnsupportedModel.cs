using Microsoft.AI.Local.Providers;

namespace Microsoft.AI.Local.Windows.Providers;

/// <summary>
/// The base class of the handles a Windows task provider package ships in its platform-neutral (<c>net8.0</c>) build:
/// every call reports <see cref="ModelAvailabilityStatus.NotSupportedOnPlatform"/>. This lets cross-platform projects
/// reference Windows models without <c>#if</c> and fall back to another model.
/// </summary>
/// <remarks>This type is intended for Windows task provider packages.</remarks>
/// <typeparam name="TClient">The inference client contract.</typeparam>
public abstract class WindowsUnsupportedModel<TClient> : LocalModelBase<TClient>
    where TClient : class
{
    private static readonly ModelAvailability Unsupported = new(
        ModelAvailabilityStatus.NotSupportedOnPlatform,
        OperatingSystem.IsWindows()
            ? "Windows inbox AI models need an app built for a Windows target framework (net8.0-windows10.0.19041.0 or later). This app uses the platform-neutral build of the Windows provider packages."
            : "Windows inbox AI models are only available on Windows.");

    /// <summary>Initializes a new instance of the <see cref="WindowsUnsupportedModel{TClient}"/> class.</summary>
    /// <param name="descriptor">The model descriptor from the Windows catalog manifest.</param>
    protected WindowsUnsupportedModel(LocalModelDescriptor descriptor)
        : base(descriptor)
    {
    }

    /// <inheritdoc/>
    protected override ValueTask<ModelAvailability> GetAvailabilityCoreAsync(CancellationToken cancellationToken) => new(Unsupported);

    /// <inheritdoc/>
    protected override Task<ModelAvailability> EnsureReadyCoreAsync(AcquisitionProgressReporter progress, CancellationToken cancellationToken) =>
        Task.FromResult(Unsupported);

    /// <inheritdoc/>
    protected override Task<TClient> CreateClientCoreAsync(CancellationToken cancellationToken) =>
        throw new LocalModelNotSupportedException(Unsupported, Id);
}
