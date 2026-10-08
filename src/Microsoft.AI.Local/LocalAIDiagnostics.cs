using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Microsoft.AI.Local;

/// <summary>
/// The names of the <see cref="ActivitySource"/> and <see cref="Meter"/> used by local AI providers.
/// </summary>
/// <remarks>
/// Subscribe with OpenTelemetry using <c>.AddSource(LocalAIDiagnostics.SourceName)</c> and
/// <c>.AddMeter(LocalAIDiagnostics.SourceName)</c>. Model acquisition emits an <c>ensure_ready</c> activity and
/// <c>microsoft.ai.local.acquisition.duration</c> histogram tagged with the model and provider. Inference telemetry
/// follows the OpenTelemetry generative AI conventions through <c>Microsoft.Extensions.AI</c>'s
/// <c>UseOpenTelemetry()</c>.
/// </remarks>
public static class LocalAIDiagnostics
{
    /// <summary>The name of the activity source and meter.</summary>
    public const string SourceName = "Microsoft.AI.Local";

    internal static ActivitySource ActivitySource { get; } = new(SourceName);

    internal static Meter Meter { get; } = new(SourceName);

    internal static Histogram<double> AcquisitionDuration { get; } = Meter.CreateHistogram<double>(
        "microsoft.ai.local.acquisition.duration",
        unit: "s",
        description: "Duration of model acquisition (EnsureReadyAsync).");
}
