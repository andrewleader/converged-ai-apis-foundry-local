namespace Microsoft.AI.Local;

/// <summary>
/// A normalized, provider-independent progress update reported by <see cref="ILocalModel.EnsureReadyAsync"/>.
/// </summary>
/// <param name="Stage">The current acquisition stage.</param>
/// <param name="Fraction">Progress within <paramref name="Stage"/>, from 0.0 to 1.0.</param>
/// <param name="OverallFraction">Best-effort progress across all stages, from 0.0 to 1.0. Never decreases.</param>
/// <param name="Detail">Optional detail, such as the name of the execution provider being downloaded.</param>
public readonly record struct ModelAcquisitionProgress(
    AcquisitionStage Stage,
    double Fraction,
    double OverallFraction,
    string? Detail = null);

/// <summary>
/// A stage of model acquisition.
/// </summary>
public enum AcquisitionStage
{
    /// <summary>Initializing the provider or checking prerequisites.</summary>
    Preparing,

    /// <summary>Downloading or registering runtime components such as execution providers.</summary>
    DownloadingRuntime,

    /// <summary>Downloading the model (by the provider or by the operating system).</summary>
    DownloadingModel,

    /// <summary>Loading the model into memory.</summary>
    Loading,
}
